# The whole system as one image: the API with the screens inside it.
#
# One image, not two, because the API serves the built screens itself (specification
# section 15). That is the same arrangement as the factory server — one address, no
# second web server, no cross-origin requests — so what the factory tries in the cloud
# behaves the way the real thing will.
#
# Built in three stages. Only the last one is kept, so the finished image carries no
# compiler, no npm, and none of the source.
#
#   docker build -t colors-erp .
#   docker run -p 8080:8080 -e ConnectionStrings__ColorsDb="..." -e Jwt__SigningKey="..." colors-erp


# ---------------------------------------------------------------- the screens
FROM node:24-alpine AS screens
WORKDIR /screens

# The lock file alone first. These two lines change only when a package changes, so the
# long install is reused while the screens themselves are edited.
COPY Frontend/package.json Frontend/package-lock.json ./

# `ci`, not `install`: exactly what the lock file says, so what the factory tries is what
# was tested rather than whatever was newest this morning.
RUN npm ci

COPY Frontend/ ./
RUN npm run build


# ---------------------------------------------------------------- the API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /api

# Same idea: the project files first, so the package restore is reused whenever only
# the C# changes.
#
# These two shared files come first and are not optional. Directory.Build.props is where
# the .NET version is set, and Directory.Packages.props is where every package version
# is pinned — no .csproj states either for itself. Without them restore reads a project
# with no framework at all and stops with "The TargetFramework value '' was not
# recognized", which sounds like a broken project file and is really a missing one.
COPY Backend/Directory.Build.props Backend/Directory.Packages.props Backend/

COPY Backend/src/Colors.Domain/Colors.Domain.csproj        Backend/src/Colors.Domain/
COPY Backend/src/Colors.Application/Colors.Application.csproj Backend/src/Colors.Application/
COPY Backend/src/Colors.Infrastructure/Colors.Infrastructure.csproj Backend/src/Colors.Infrastructure/
COPY Backend/src/Colors.Api/Colors.Api.csproj              Backend/src/Colors.Api/
RUN dotnet restore Backend/src/Colors.Api/Colors.Api.csproj

COPY Backend/ Backend/
RUN dotnet publish Backend/src/Colors.Api/Colors.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /published


# ---------------------------------------------------------------- what actually runs
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=api /published ./

# Into wwwroot, which is where the API looks for them.
COPY --from=screens /screens/dist ./wwwroot

# The log folder, made now and handed to the account that will run.
#
# The application creates this itself on the factory server, where it runs as a service
# with rights to its own folder. Here it runs as a plain user who does not own /app, so
# creating it would be refused and the application would stop before it started.
#
# Inside a container the file itself is of little use — it disappears with the container,
# and the cloud host shows the console output instead. It is written anyway so that
# nothing about how the system logs depends on where it is running.
RUN mkdir -p /app/logs && chown -R $APP_UID /app/logs

# Production: no demonstration accounts and no administrator password reset. Both are
# fenced off outside development on purpose (specification section 3).
ENV ASPNETCORE_ENVIRONMENT=Production

# A default for running it by hand. A cloud host sets PORT instead and the application
# follows it (see HostingExtensions).
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# There is always something in front of this in the cloud, terminating HTTPS.
ENV Hosting__BehindProxy=true

# The trial database starts empty and holds nothing but practice. On the factory server
# this stays off and Migrate.ps1 is used, after a backup.
ENV Database__MigrateOnStartup=true

# Not root. A container that is broken into should not be able to rewrite its own files.
USER $APP_UID

EXPOSE 8080

# No HEALTHCHECK here on purpose. The image carries no curl or wget — nothing to ask the
# question with — and every cloud host does its own check over HTTP anyway. Point the
# host's health check at:
#
#     /health
#
# It answers 200 when it can reach the database and 503 when it cannot, which is the
# useful version of the question (specification section 15).

ENTRYPOINT ["dotnet", "Colors.Api.dll"]
