<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:wix="http://schemas.microsoft.com/wix/2006/wi">
  <xsl:output method="xml" indent="yes" />
  
  <!-- Copy everything -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>
  
  <!-- Exclude SpiceServiceTray.exe, SpiceServiceTray.dll, and McpRemote.exe components -->
  <xsl:template match="wix:Component[contains(wix:File/@Source, 'SpiceServiceTray.exe')]" />
  <xsl:template match="wix:Component[contains(wix:File/@Source, 'SpiceServiceTray.dll')]" />
  <xsl:template match="wix:Component[contains(wix:File/@Source, 'McpRemote.exe')]" />
  
  <!-- Also exclude ComponentRefs to these components -->
  <xsl:template match="wix:ComponentRef[contains(@Id, 'SpiceServiceTray.exe')]" />
  <xsl:template match="wix:ComponentRef[contains(@Id, 'SpiceServiceTray.dll')]" />
  <xsl:template match="wix:ComponentRef[contains(@Id, 'McpRemote.exe')]" />
</xsl:stylesheet>

