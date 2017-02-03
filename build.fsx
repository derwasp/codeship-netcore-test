#I @"./packages/build/FAKE/tools"
#I @"./packages/build/AWSSDK.ECR/lib/net45"
#I @"./packages/build/AWSSDK.Core/lib/net45"

#r "System.IO.Compression.dll"
#r "FakeLib.dll"
#r "Newtonsoft.Json.dll"
#r "AWSSDK.Core.dll"
#r "AWSSDK.ECR.dll"

#load @"paket-files/build/derwasp/Fake.Extra/Docker.fs"

open System
open System.IO
open Fake
open Fake.DockerHelper
open Fake.GitVersionHelper

open Newtonsoft.Json.Linq

    module Defaults =
        let internalPort = 5000us

    module ScriptVars =
        let version() = GitVersion (fun p -> { p with ToolPath = findToolInSubPath "GitVersion.exe" currentDirectory})

let dockerDefaultImage = {
    Registry = None
    Repository = ""
    Tag = None }

Target "Trace" <| fun _ ->
    if not <| DotNetCli.isInstalled() then
        trace "Dotnet CLI is not installed"
    else
        tracefn "Dotnet CLI is installed. Version %s" <| DotNetCli.getVersion()

    "--version"
    |> CustomExec
    |> dockerAsk
    |> fun x -> x.Messages
    |> Seq.head
    |> trace


Target "Clean" <| fun _ ->
    !! "artifacts"
    ++ "src/*/bin"
    ++ "tests/*/bin"
    ++ "src/*/obj"
    ++ "tests/*/obj"
    |> DeleteDirs

Target "SetVersion" <| fun _ ->
    let version = ScriptVars.version()
    tracefn "Gitversion is %s" version.FullSemVer
    SetBuildNumber version.NuGetVersion

    !! "./**/project.json"
    |> Seq.iter(DotNetCli.SetVersionInProjectJson version.NuGetVersion)

Target "Restore" <| fun _ ->
    DotNetCli.Restore id

Target "Compile" <| fun _ ->
    let buildProject project =
        DotNetCli.Build (fun p -> { p with Project = project})

    !! "./**/project.json"
    |> Seq.iter buildProject

Target "Test" <| fun _ ->
    let testProject project =
        DotNetCli.Test (fun p -> { p with Project = project})

    !! "./tests/**/project.json"
    |> Seq.iter testProject

Target "Publish" <| fun _ ->
    let publishProject proj =
        tracefn "Building %s" proj
        let projectDirectory = Path.GetDirectoryName proj
        let projectName = Path.GetFileName projectDirectory
        let outputDirectory = "artifacts" </> projectName

        DotNetCli.Publish (fun p ->
                        { p with
                              Output = outputDirectory </> "bin"
                              Project = proj })

        !! (projectDirectory </> "Docker/*.Dockerfile")
        |> Seq.iter (fun dockerFile ->
                        tracefn "Found Dockerfile %s. Copying to %s" dockerFile outputDirectory
                        dockerFile
                        |> CopyFile ("artifacts" </> projectName))

    !! "src/**/project.json"
    |> Seq.iter publishProject

let getValuesForDockerFile dockerFile =
    let workingDirectory = Path.GetDirectoryName dockerFile
    let repo = Path.GetFileNameWithoutExtension dockerFile
    let image = { dockerDefaultImage with Repository = repo }
    image, dockerFile, workingDirectory

Target "DockerClean" <| fun _ ->
    let wellKnownImages = !! "./artifacts/**/*.Dockerfile"
                          |> Seq.map getValuesForDockerFile
                          |> Seq.map (fun (image, _, _) -> image.Repository)

    let containsAnyNames (names : string seq) (item : string) =
        let result = names
                     |> Seq.fold
                        (fun acc itm -> if acc then acc else item.Contains(itm))
                        false
        tracefn "Item %s is found in %s = %b" item (wellKnownImages |> String.concat "+") result
        result

    "images --format \"{{.Repository}}:{{.Tag}}\""
    |> CustomExec
    |> dockerAsk
    |> fun x -> x.Messages
    |> Seq.filter (containsAnyNames wellKnownImages)
    |> Seq.iter (sprintf "rmi %s -f" >> CustomExec >> dockerDo)

let healthCheck uri =
  match REST.ExecuteGetCommand null null uri with
  | null -> Choice2Of2 503
  | resp -> Choice1Of2 resp

let testDocker dockerImageName =
  let label = dockerRun Defaults.internalPort dockerImageName List.empty
  try
    let remoteIp = dockerGetIpAddress label
    let rec doHealthCheck count =
      let statusCode = healthCheck (sprintf "http://%s:%i/v1/diagnostics" remoteIp Defaults.internalPort)
      match statusCode with
      | Choice1Of2 body ->
        Choice1Of2 body
      | Choice2Of2 status ->
        printfn "Bad healthcheck (Status %i). Waiting 1s." status
        System.Threading.Thread.Sleep 1000
        if count = 0 then
          Choice2Of2 ()
        else
          doHealthCheck (count - 1)
    let attempts = 60
    match doHealthCheck attempts with
    | Choice1Of2 body ->
      printfn "Successful healthcheck!\n%s" body
    | Choice2Of2 _ ->
      failwithf "Unable to get successful healthcheck after %i attempts" attempts
  finally
    try
      dockerDo (Stop label)
    finally
      dockerDo (Remove label)

let tagPushDelete image tag =
  dockerDo (Tag (image, tag))
  try
    dockerDo (Push tag)
  finally
    dockerDo (RemoveImage tag)

Target "DockerBuild" <| fun _ ->
    !! "./artifacts/**/*.Dockerfile"
    |> Seq.map getValuesForDockerFile
    |> Seq.iter
        (fun (image, dockerFile, workingDirectory) ->
            dockerDo <| Build (image, workingDirectory, Some(dockerFile)))

Target "DockerTest" <| fun _ ->
    !! "./artifacts/**/*.Dockerfile"
    |> Seq.map getValuesForDockerFile
    |> Seq.iter (fun (image, _, _) -> testDocker image)


"Trace"
==> "Clean"
==> "SetVersion"
==> "Restore"
==> "Compile"
==> "Publish"
==> "DockerClean"
==> "DockerBuild"
==> "DockerTest"

RunTargetOrDefault "DockerTest"