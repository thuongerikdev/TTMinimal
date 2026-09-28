# -----------------------------------------------------------
# Creates a Docker image by building and publishing 
# the source within the container
# -----------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# The prebuilt Smartstore.ModuleBuilder.dll targets net9.0; the SDK image only
# ships the .NET 10 runtime, so let it roll forward to the next major version.
ENV DOTNET_ROLL_FORWARD=Major

# Copy solution and source
ARG SOLUTION=Smartstore.sln
WORKDIR /app
COPY $SOLUTION ./
COPY src/ ./src
COPY test/ ./test
COPY nuget.config ./

# Create Modules dir if missing
RUN mkdir /app/src/Smartstore.Web/Modules -p -v

# Build (NuGet packages kept in a BuildKit cache mount between builds)
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet build $SOLUTION -c Release

# Publish (same cache mount: --no-restore needs the packages restored above)
WORKDIR /app/src/Smartstore.Web
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish Smartstore.Web.csproj -c Release -o /app/release/publish \
	--no-self-contained \
	--no-restore

# Custom themes: copy explicitly and fail the build if the storefront theme is missing,
# instead of silently falling back to Flex at runtime.
RUN cp -r /app/src/Smartstore.Web/Themes/TTMinimal /app/release/publish/Themes/ && \
    test -f /app/release/publish/Themes/TTMinimal/theme.config

# Build Docker image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
EXPOSE 80
EXPOSE 443
ENV ASPNETCORE_URLS="http://+:80;https://+:443"

# Install wkhtmltopdf BEFORE copying the app, so this slow layer stays cached
# and is only rebuilt when the script or base image changes (not on code edits)
COPY install-wkhtmltopdf.sh /tmp/
# Strip CR in case the script was checked out with Windows line endings
RUN sed -i 's/\r$//' /tmp/install-wkhtmltopdf.sh && \
    chmod +x /tmp/install-wkhtmltopdf.sh && \
    /tmp/install-wkhtmltopdf.sh && \
    rm /tmp/install-wkhtmltopdf.sh

WORKDIR /app
COPY --from=build /app/release/publish .

ENTRYPOINT ["./Smartstore.Web", "--urls", "http://0.0.0.0:80"]
