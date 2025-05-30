FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development

# Update ubuntu
RUN apt update
RUN apt upgrade -y

RUN apt install -y wget apt-transport-https libc6-dev libunwind8 curl git locales unzip

# .NET SDK MSBuild requires US.UTF-8 locale to execute tasks (see https://github.com/Microsoft/msbuild/issues/4194)
RUN locale-gen en_US.UTF-8 

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
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS production


COPY --from=development /opt/tap /opt/tap

RUN chmod -R +w /opt/tap
RUN chmod +x /opt/tap/tap

RUN apt-get update
RUN apt-get install -y --no-install-recommends libunwind8 curl git 
RUN rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/tap:${PATH}"
ENV TAP_PATH="/opt/tap"

### Hardened security - will run as non-root user by default 
FROM production AS production-hardened

# Create opentap user and group
# - set home dir, since OpenTAP expects home dir to maintain cache
# - no default shell, as user should only run tap
ARG USER=opentap UID=1001 GROUP=opentap GID=1001 DUMMYSHELL=/bin/false
ARG HOMEDIR=/home/${USER}
ARG OPENTAP_CACHE=${HOMEDIR}/.local/share/OpenTap
RUN groupadd --gid=$GID $GROUP && \
  useradd --uid $UID --gid $GID --shell $DUMMYSHELL --home-dir $HOMEDIR $USER && \
  install --directory --owner $USER --group $GROUP --mode u=rwx,g=rwx,o= $HOMEDIR && \
  install --directory --owner $USER --group $GROUP --mode u=rwx,g=rwx,o= $OPENTAP_CACHE

# Set ownership and permissions for OpenTAP installation
WORKDIR $TAP_PATH
RUN chown --recursive "${UID}:${GID}" .
RUN chmod --recursive u=rwx,g=rwx,o= .

ENTRYPOINT [ "tap" ]

# Create mount point to persist OpenTAP cache across restarts
VOLUME $OPENTAP_CACHE

# Must use $UID as $USER could potentially alias root user
USER $UID
