<!--
Copyright (c) 2007, LShift Ltd. <query@lshift.net>
Copyright (c) 2007, Tony Garnock-Jones <tonyg@kcbbs.gen.nz>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
-->
<xsl:stylesheet version="1.0"
		xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:include href="utils.xsl"/>

  <xsl:template match="/namespace">
    <html>
      <head>
	<title>Namespace <xsl:call-template name="namespace-printname"/></title>
	<link rel="stylesheet" rev="stylesheet" href="style.css" type="text/css"/>
      </head>
      <body>
	<div class="upLinks">
	  <a href="index.html">Index</a>
	</div>
	<h1>Namespace <xsl:call-template name="namespace-printname"/></h1>
	<xsl:call-template name="format-doc">
	  <xsl:with-param name="doc" select="doc"/>
	</xsl:call-template>
	<h2>Types</h2>
	<table class="typesTable">
	  <tr>
	    <th class="typeNameHeader">Type</th>
	    <th class="typeSummaryHeader">Summary</th>
	  </tr>
	  <xsl:for-each select="typedoc">
	    <tr>
	      <td class="typeName">
		<xsl:call-template name="typeref">
		  <xsl:with-param name="typeref" select="type"/>
		</xsl:call-template>
	      </td>
	      <td class="typeSummary">
		<xsl:call-template name="format-summary">
		  <xsl:with-param name="doc" select="doc"/>
		</xsl:call-template>
	      </td>
	    </tr>
	  </xsl:for-each>
	</table>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>
