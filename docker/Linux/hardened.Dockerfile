FROM mcr.microsoft.com/dotnet/sdk:6.0-focal as build

WORKDIR /src

# Install TAP from pre-built package
RUN apt update
RUN apt install unzip --yes
COPY OpenTAP.Linux.TapPackage OpenTAP.Linux.TapPackage
RUN unzip OpenTAP.Linux.TapPackage -d ./tap
# Dummy ID to void telemetry
RUN mkdir --parents /root/.local/share/OpenTap
RUN echo 11111111-1111-1111-1111-111111111111 > /root/.local/share/OpenTap/OpenTapGeneratedId

# Test TAP
RUN dotnet ./tap/tap.dll
RUN dotnet ./tap/tap.dll package list --verbose

################## DOTNET CORE RUNTIME #########################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal

# Create non-root system user and group
ARG USER=opentap UID=101 GROUP=opentap GID=101 DUMMYSHELL=/bin/false
ARG USERHOME="/home/${USER}"
RUN groupadd --system --gid=$GID $GROUP && \
  useradd --system --uid $UID --gid $GID --shell $DUMMYSHELL $USER && \
  install --directory --mode 0755 --owner $UID --group $GID $USERHOME

# Copy tap installation to user home
ENV TAP_PATH="${USERHOME}/.tap"
WORKDIR $TAP_PATH
COPY --from=build /src/tap .
# Fix ownership and permissions
RUN chown --recursive "${UID}:${GID}" .
RUN chmod --recursive 0755 .

ENV PATH="${PATH}:${TAP_PATH}"
# Enable detection of running in a container
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT [ "tap" ]

USER $USER
