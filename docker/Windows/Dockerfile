FROM mcr.microsoft.com/dotnet/framework/sdk:4.7.2

# Download OpenTAP
COPY OpenTAP.exe .

# Install OpenTAP
RUN cmd /C "OpenTAP.exe /quiet /VERYSILENT"
RUN cmd /C "setx PATH '%PATH%C:\Program Files\OpenTAP;'"
RUN mkdir C:\Users\ContainerAdministrator\AppData\Local\OpenTAP
RUN echo 11111111-1111-1111-1111-111111111111 > C:\Users\ContainerAdministrator\AppData\Local\OpenTAP\OpenTapGeneratedId
