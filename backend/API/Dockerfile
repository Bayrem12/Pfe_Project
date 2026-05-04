#Start a build stage using .NET8 SDK image
#AS build means give this stage a name "build"
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

#set src as work directory in the container 
WORKDIR /src 

#copy API project file into /src
COPY API/API.csproj API/

#copy Application project file into /src
COPY Application/Application.csproj Application/

#copy Domain project file into /src
COPY Domain/Domain.csproj Domain/

#copy Persistance project file into /src
Copy Persistance/Persistance.csproj Persistance/

#restore NuGet packages for the API project
RUN dotnet restore API/API.csproj

#Copy the rest of the backend source-code into the container
COPY . .

#Build & publish the API in release mode into /app/publish
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

#start a second, smaller runtime-only image used to run the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 

#set app as the runtime directory
WORKDIR /app

#copy the published app from the build stage into the runtime stage
COPY --from=build /app/publish .

#Tell the container to start the app with dotnet API.dll
ENTRYPOINT ["dotnet", "API.dll"]



