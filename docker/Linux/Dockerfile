FROM mcr.microsoft.com/dotnet/sdk:6.0-focal as development

# Update ubuntu
RUN apt update
RUN apt upgrade -y

# Install dotnet core 2.1
RUN apt install -y wget
RUN apt install apt-transport-https -y

# TAP dotnet core 2.1 dependency
RUN apt install libc6-dev libunwind8 curl git locales -y

# .NET SDK MSBuild requires US.UTF-8 locale to execute tasks (see https://github.com/Microsoft/msbuild/issues/4194)
RUN locale-gen en_US.UTF-8 

# Install TAP
RUN apt install unzip -y
COPY OpenTAP.Linux.TapPackage OpenTAP.Linux.TapPackage
RUN unzip OpenTAP.Linux.TapPackage -d /opt/tap
RUN chmod -R +w /opt/tap
RUN chmod +x /opt/tap/tap
ENV PATH="/opt/tap:${PATH}"
ENV TAP_PATH="/opt/tap"
RUN mkdir -p /root/.local/share/OpenTap
RUN echo 11111111-1111-1111-1111-111111111111 > /root/.local/share/OpenTap/OpenTapGeneratedId

# Test TAP
RUN tap -h
RUN tap package list -v

################## DOTNET CORE RUNTIME #########################################
FROM mcr.microsoft.com/dotnet/runtime:6.0-focal as production


COPY --from=development /opt/tap /opt/tap

RUN chmod -R +w /opt/tap
RUN chmod +x /opt/tap/tap

RUN apt-get update && apt-get install -y --no-install-recommends libunwind8 curl git && rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/tap:${PATH}"
ENV TAP_PATH="/opt/tap"

FROM production AS production-hardened

# Create non-root system user and group
ARG USER=opentap UID=101 GROUP=opentap GID=101 DUMMYSHELL=/bin/false
ARG USERHOME="/home/${USER}"
RUN groupadd --system --gid=$GID $GROUP && \
  useradd --system --uid $UID --gid $GID --shell $DUMMYSHELL $USER

WORKDIR $TAP_PATH
# Fix ownership and permissions
RUN chown --recursive "${UID}:${GID}" .
RUN chmod --recursive u=rwx,g=rwx,o= .

ENTRYPOINT [ "tap" ]

USER $USER
