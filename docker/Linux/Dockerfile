FROM ubuntu:18.04 as development

# Update ubuntu
RUN apt update
RUN apt upgrade -y

# Install dotnet core 2.1
RUN apt install -y wget
RUN wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt install apt-transport-https -y
RUN apt update
RUN apt install dotnet-sdk-2.1 -y

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

# Test TAP
RUN tap -h
RUN tap package list -v

################## DOTNET CORE RUNTIME #########################################
FROM mcr.microsoft.com/dotnet/core/runtime:2.1.12-bionic as production

COPY --from=development /opt/tap /opt/tap

RUN chmod -R +w /opt/tap
RUN chmod +x /opt/tap/tap

RUN apt update
# TAP dotnet core 2.1 dependency
RUN apt install libc6-dev libunwind8 curl git -y

ENV PATH="/opt/tap:${PATH}"
ENV TAP_PATH="/opt/tap"
