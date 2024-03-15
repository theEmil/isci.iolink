FROM mcr.microsoft.com/dotnet/runtime:8.0


RUN apt-get update && apt-get install -y libpcap-dev

# Working directory anlegen, Dateien kopieren und Berechtigungen setzen
WORKDIR /app
COPY ./publish ./
RUN chmod +x "./openDCOSIoLinkModul"

# Einstellungen übernehmen
COPY ./settings.json ./settings.json

# Umgebungsvariablen setzen
ENV "ISCI_Identifikation"="isci.IOLink"
ENV "ISCI_OrdnerAnwendungen"="/app/Anwendungen"
ENV "ISCI_OrdnerDatenstrukturen"="/app/Datenstrukturen"

# Umgebungsvariablen, die auf dem System angelegt werden müssen:
# ISCI_Ressource=XXX
# ISCI_Anwendung=XXX
#
# Die beiden Ordner in den Umgebungsvariablen vom Host-System müssen eingebunden werden
# Es muss außerdem eine Konfiguration "isci.IOLink.json" unter dem Pfad "Anwendungen/Anwendungen/Konfigurationen/isci.IOLink.json" vorhanden sein
# 
# Ports die durchgereicht werden müssen:
# hier keine
#
# Ein Netzwerkinterface muss durchgereicht werden, damit libpcap funktioniert

ENTRYPOINT ["./openDCOSIoLinkModul"]