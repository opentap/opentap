FROM mcr.microsoft.com/dotnet/framework/sdk:4.7.2

# Download OpenTAP
COPY OpenTAP.exe .

# Install OpenTAP
RUN cmd /C "OpenTAP.exe /quiet /VERYSILENT"
RUN cmd /C "setx PATH '%PATH%C:\Program Files\OpenTAP;'"
