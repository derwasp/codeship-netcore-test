FROM microsoft/dotnet:latest
COPY bin/ /root/
EXPOSE 5000/tcp
WORKDIR /root/
ENTRYPOINT ["dotnet", "/root/TestApp.dll"]