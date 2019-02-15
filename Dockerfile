FROM ubuntu:18.04

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
RUN apt install libc6-dev libunwind8 curl git -y

# Install dependency of TAP dependency "Git2Sharp" library
RUN apt install -y libcurl3

# Install TAP
RUN apt install unzip -y
COPY TAPLinux.TapPackage TAPLinux.TapPackage
RUN unzip TAPLinux.TapPackage -d /opt/tap
RUN chmod -R +w /opt/tap
RUN chmod +x /opt/tap/tap
ENV PATH="/opt/tap:${PATH}"
ENV TAP_PATH="/opt/tap"

# Test TAP
RUN tap -h
RUN tap package list -v

# Run a test plan
#COPY ../../Tap/Engine.UnitTests/TestTestPlans .
#RUN tap run testMultiReferencePlan.TapPlan -v