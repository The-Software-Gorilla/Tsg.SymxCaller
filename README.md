# Tsg.SymxCaller

This code is used with the Tsg.Rdc repository to handle calling SymXChange from an on-prem server.
The last leg of the call from the on-prem server to SymXChange has to be initiated from an on-prem
server with an IP address that is whitelisted with SymXChange. It also has to make the call across
a VPN connection to the SymXChange endpoint that cannot be configured to connect from Azure directly.

This code therefore listens on the `symx-outbound` queue for messages from the Tsg.Rdc application
running in Azure, and then makes the call to SymXChange on behalf of that application.

Once the call to SymXChange is complete, it puts the response message onto the response queue that
was specified in the original message from Tsg.Rdc. It also updates the status of the request
in the Tsg.Rdc database.

This code is intended to run as a Docker container in an on-prem server environment. There is a Dockerfile
and a Docker compose yml file included in the repository to build the container image.

For more information about the overall architecture and how this component fits in, please refer to the
[documentation in the Tsg.Rdc repository](https://github.com/The-Software-Gorilla/Tsg.Rdc/blob/main/README.md).

## Building the Docker Image
To build the Docker image, run the following command in the root of the repository:

```bash
docker compose build
```
To upload the image to a docker desktop, use the following command:

```bash
docker compose up -d
```

