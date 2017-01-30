#!/usr/bin/env sh

echo "Running inside dind, checking docker version"
echo ""

docker --version

echo "This script runs a .NET core application, which exposes 5000 port"
echo ""
echo "As a health check, curl will be used to get the data from GET /v1/diagnostics resource. It should return an assembly version"
echo "until loop will retry 6 times and then quit. If everything went correct, we should see the version printed as the last step of the script."
echo ""

echo "First, lets list all the containers running now"

echo ""
docker ps
echo ""

echo "docker network"
echo ""
docker network ls
echo ""

echo "Trying to run a container from an image which is accessible from docker hub: derwasp/test-app."
echo ""

CONTAINERID="$(docker run -d -p 5000 derwasp/test-app)"

echo "Started container: ${CONTAINERID}."
echo "docker ps:"
echo ""

docker ps

echo ""
echo "Trying to find a local (host) port which is mapped to container's 5000. [Not used in this script]"
echo ""

PORT="$(docker inspect -f '{{(index (index .NetworkSettings.Ports "5000/tcp") 0).HostPort}}' ${CONTAINERID})"
echo "Port = ${PORT}"

echo ""
echo "Trying to find an IP address of the container."
echo ""
IPADDRESS="$(docker inspect -f '{{.NetworkSettings.IPAddress}}' ${CONTAINERID})"
echo "IpAddress = ${IPADDRESS}"

CURL_RESULT="nothing"

curl_the_service () {
 SERVICE_URL=$1:5000/v1/diagnostics
 echo "trying to call the service ${SERVICE_URL}"
 CURL_RESULT="$(curl -sS ${SERVICE_URL})"
}

NEXT_WAIT_TIME=0
until curl_the_service ${IPADDRESS} || [ $NEXT_WAIT_TIME -eq 6 ]; do
   NEXT_WAIT_TIME=$(( NEXT_WAIT_TIME + 1 ))
   sleep $NEXT_WAIT_TIME
done

docker stop ${CONTAINERID}

echo "Result is $CURL_RESULT"
