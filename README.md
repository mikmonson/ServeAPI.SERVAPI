SERVAPI
1) to run on Linux:
 - install dotnet, .... (see docs)
 - "dotnet run" inside the project folder or "dotnet publish" -> compiled executable can be run independently but the server will only listed on localhost:5001 (can be solved with reverse proxy)
 2) in Windows SERVAPI should run as stand-alone (doesn't work with IIS Express). It also listens to localhost:5001 only.
 3) self signed certificate fo the server is stored in the project folder/certs. Thumpbrints of client certificates are stored in the text file.
 
Clinet:
Python client:
1) client should us own certificate (pem) and private key;
2) client should send GET request https://<server>:5001/hello?sn=<serial number>,mac=<mac-address>,......  Same S/N should be CN=<S/N> in client certificate.
