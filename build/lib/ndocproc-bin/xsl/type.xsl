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

  <xsl:param name="config"/>
  
  <xsl:variable name="googleNonlocalTypes" 
                select="document($config)/options/option[@googlenonlocaltypes]/@googlenonlocaltypes"/>

  <xsl:include href="utils.xsl"/>

  <xsl:template name="method-summary">
    <xsl:param name="method"/>
    <tr>
      <xsl:if test="not(/typedef/@interface)">
	<td class="methodFlags">
	  <xsl:call-template name="item-flags">
	    <xsl:with-param name="node" select="$method"/>
	  </xsl:call-template>
	</td>
      </xsl:if>
      <td class="methodName">
	<a href="#method-{$method/@anchor}">
	  <xsl:call-template name="method-signature">
	    <xsl:with-param name="method" select="$method"/>
	  </xsl:call-template>
	</a>
      </td>
      <td class="methodSummary">
	<xsl:call-template name="format-summary">
	  <xsl:with-param name="doc" select="$method/doc"/>
	</xsl:call-template>
      </td>
    </tr>
  </xsl:template>

  <xsl:template name="parameters-table-rows">
    <xsl:param name="parameters"/>
    <xsl:if test="$parameters">
      <tr>
	<th class="parametersHeader">Parameters</th>
	<td class="parameters">
	  <table class="parametersTable">
	    <tr>
	      <th class="parameterNameHeader">Name</th>
	      <th class="parameterTypeHeader">Type</th>
	    </tr>
	    <xsl:for-each select="$parameters">
	      <tr>
		<td class="parameterName"><xsl:value-of select="@name"/></td>
		<td class="parameterType"><xsl:call-template name="parameter-type">
		  <xsl:with-param name="parameter" select="."/>
		  <xsl:with-param name="isref" select="'true'"/>
		</xsl:call-template></td>
	      </tr>
	    </xsl:for-each>
	  </table>
	</td>
      </tr>
    </xsl:if>
  </xsl:template>

  <xsl:template name="method-detail">
    <xsl:param name="method"/>
    <div class="methodDetail">
      <a name="method-{$method/@anchor}"></a>
      <h3><xsl:value-of select="$method/@leaf"/></h3>
      <p>
	<xsl:if test="not(/typedef/@interface)">
	  <code>
	    <xsl:call-template name="item-flags">
	      <xsl:with-param name="node" select="$method"/>
	    </xsl:call-template>
	  </code>
	</xsl:if>
	<xsl:call-template name="method-signature">
	  <xsl:with-param name="method" select="$method"/>
	</xsl:call-template>
      </p>
      <table class="methodDetailTable">
	<xsl:if test="$method/returns">
	  <xsl:if test="not(/typedef/@interface)">
	    <tr>
	      <th class="methodFlagsHeader">Flags</th>
	      <td class="methodFlags"><xsl:call-template name="item-flags"><xsl:with-param name="node" select="$method"/></xsl:call-template></td>
	    </tr>
	  </xsl:if>
	  <tr>
	    <th class="returnTypeHeader">Return type</th>
	    <td class="returnType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="$method/returns/type"/></xsl:call-template></td>
	  </tr>
	</xsl:if>

	<xsl:call-template name="parameters-table-rows">
	  <xsl:with-param name="parameters" select="$method/parameters/parameter"/>
	</xsl:call-template>
      </table>
      <div class="methodDetailDoc">
	<xsl:call-template name="format-doc">
	  <xsl:with-param name="doc" select="$method/doc"/>
	</xsl:call-template>
      </div>
    </div>
  </xsl:template>

  <xsl:template name="delegate-detail-table">
    <xsl:param name="invoker"/>
    <table class="delegateDetailTable">
      <tr>
	<th class="returnTypeHeader">Return type</th>
	<td class="returnType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="$invoker/returns/type"/></xsl:call-template></td>
      </tr>
      <xsl:call-template name="parameters-table-rows">
	<xsl:with-param name="parameters" select="$invoker/parameters/parameter"/>
      </xsl:call-template>
    </table>
  </xsl:template>

  <xsl:template match="defaultconstructor" mode="type-constraint">new()</xsl:template>
  <xsl:template match="notnullablevaluetype" mode="type-constraint">struct</xsl:template>
  <xsl:template match="referencetype" mode="type-constraint">class</xsl:template>
  <xsl:template match="type" mode="type-constraint"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="."/></xsl:call-template></xsl:template>

  <xsl:template match="/typedef">
    <html>
      <head>
	<title>
	  <xsl:call-template name="item-flags"><xsl:with-param name="node" select="."/></xsl:call-template>
	  <xsl:call-template name="type-alias">
	    <xsl:with-param name="typeref" select="type"/>
	    <xsl:with-param name="declaringtype" select="/.."/>
	  </xsl:call-template>
	</title>
	<link rel="stylesheet" rev="stylesheet" href="style.css" type="text/css"/>
      </head>
      <body>
	<div class="upLinks">
	  <a href="index.html">Index</a> |
	  Namespace <a href="namespace-{@namespace}.html"><xsl:call-template name="namespace-printname"><xsl:with-param name="namespace-name" select="@namespace"/></xsl:call-template></a>
	</div>
	<h1>
	  <xsl:call-template name="item-flags"><xsl:with-param name="node" select="."/></xsl:call-template>
	  <xsl:call-template name="type-alias">
	    <xsl:with-param name="typeref" select="type"/>
	    <xsl:with-param name="declaringtype" select="/.."/>
	  </xsl:call-template>
	</h1>
	<ul>
	  <xsl:for-each select="type/genericarguments/type[typeconstraints/*]">
	    <li>where <code><xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="."/></xsl:call-template> : <xsl:for-each select="typeconstraints/*"><xsl:apply-templates select="." mode="type-constraint"/><xsl:if test="position() != last()">, </xsl:if></xsl:for-each></code></li>
	  </xsl:for-each>
	  <xsl:if test="type/declaringtype">
	    <li>declared within <xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type/declaringtype/type"/></xsl:call-template></li>
	  </xsl:if>
	  <xsl:if test="extends/class and extends/class/type/@name != 'System.Object'">
	    <li>extends <xsl:call-template name="typeref"><xsl:with-param name="typeref" select="extends/class/type"/></xsl:call-template></li>
	  </xsl:if>
	  <xsl:for-each select="extends/interface">
	    <li>implements <xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></li>
	  </xsl:for-each>
	</ul>
	<xsl:if test="nestedtypes/type">
	  <div class="nestedTypes">
	    <p>
	      Nested types:
	      <xsl:for-each select="nestedtypes/type">
		<xsl:call-template name="typeref">
		  <xsl:with-param name="typeref" select="."/>
		  <xsl:with-param name="declaringtype" select="/.."/>
		</xsl:call-template>
		<xsl:if test="position() != last()">, </xsl:if>
	      </xsl:for-each>
	    </p>
	  </div>
	</xsl:if>
	<xsl:if test="known-subtypes/type">
	  <div class="knownSubtypes">
	    <p>
	      Known direct subtypes:
	      <xsl:for-each select="known-subtypes/type">
		<xsl:call-template name="typeref"><xsl:with-param name="typeref" select="."/></xsl:call-template><xsl:if test="position() != last()">, </xsl:if>
	      </xsl:for-each>
	    </p>
	  </div>
	</xsl:if>

	<xsl:choose>
          <xsl:when test="extends/class/type[@name = 'System.MulticastDelegate' or
                                             @name = 'System.Delegate']">
	    <div class="delegateDetail">
	      <p>
		<code>
		  <xsl:call-template name="item-flags"><xsl:with-param name="node" select="."/></xsl:call-template>
		  <xsl:variable name="method" select="members/method[@leaf = 'Invoke']"/>
		  <xsl:if test="$method/returns">
		    <xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="$method/returns/type"/></xsl:call-template><xsl:text> </xsl:text>
		  </xsl:if>
		  <xsl:call-template name="type-alias">
		    <xsl:with-param name="typeref" select="type"/>
		  </xsl:call-template>
		  <xsl:call-template name="emit-generic-arguments">
		    <xsl:with-param name="genericarguments" select="$method/genericarguments"/>
		  </xsl:call-template>
		  <xsl:call-template name="method-signature-parameters">
		    <xsl:with-param name="method" select="$method"/>
		  </xsl:call-template>
		</code>
	      </p>
	      <xsl:call-template name="delegate-detail-table">
		<xsl:with-param name="invoker" select="members/method[@leaf = 'Invoke']"/>
	      </xsl:call-template>
	      <xsl:call-template name="format-doc">
		<xsl:with-param name="doc" select="doc"/>
	      </xsl:call-template>
	    </div>
	  </xsl:when>
	  <xsl:otherwise>
	    <xsl:call-template name="format-doc">
	      <xsl:with-param name="doc" select="doc"/>
	    </xsl:call-template>

	    <xsl:if test="members/field[not(@specialname)]">
	      <h2>Field Summary</h2>
	      <table class="fieldSummaryTable">
		<tr>
		  <th class="fieldFlagsHeader">Flags</th>
		  <th class="fieldTypeHeader">Type</th>
		  <th class="fieldNameHeader">Name</th>
		  <th class="fieldSummaryHeader">Summary</th>
		</tr>
		<xsl:for-each select="members/field[not(@specialname)]">
		  <tr>
		    <td class="fieldFlags"><xsl:call-template name="item-flags"><xsl:with-param name="node" select="."/></xsl:call-template></td>
		    <td class="fieldType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></td>
		    <td class="fieldName"><a href="#field-{@anchor}"><code><xsl:value-of select="@leaf"/></code></a></td>
		    <td class="fieldSummary">
		      <xsl:call-template name="format-summary">
			<xsl:with-param name="doc" select="doc"/>
		      </xsl:call-template>
		    </td>
		  </tr>
		</xsl:for-each>
	      </table>
	    </xsl:if>
	    <xsl:if test="members/property">
	      <h2>Property Summary</h2>
	      <table class="propertySummaryTable">
		<tr>
		  <xsl:if test="not(/typedef/@interface)">
		    <th class="propertyFlagsHeader">Flags</th>
		  </xsl:if>
		  <th class="propertyTypeHeader">Type</th>
		  <th class="propertyNameHeader">Name</th>
		  <th class="propertySummaryHeader">Summary</th>
		</tr>
		<xsl:for-each select="members/property">
		  <tr>
		    <xsl:if test="not(/typedef/@interface)">
		      <td class="propertyFlags"><xsl:call-template name="property-flags"><xsl:with-param name="node" select="."/></xsl:call-template></td>
		    </xsl:if>
		    <td class="propertyType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></td>
		    <td class="propertyName"><a href="#property-{@anchor}"><code><xsl:value-of select="@leaf"/></code></a> <span class="propertyAccessibility"> (<xsl:if test="getter">r</xsl:if><xsl:if test="setter">w</xsl:if>)</span></td>
		    <td class="propertySummary">
		      <xsl:call-template name="format-summary">
			<xsl:with-param name="doc" select="doc"/>
		      </xsl:call-template>
		    </td>
		  </tr>
		</xsl:for-each>
	      </table>
	    </xsl:if>
	    <xsl:if test="members/event">
	      <h2>Event Summary</h2>
	      <table class="eventSummaryTable">
		<tr>
		  <th class="eventTypeHeader">Type</th>
		  <th class="eventNameHeader">Name</th>
		  <th class="eventSummaryHeader">Summary</th>
		</tr>
		<xsl:for-each select="members/event">
		  <tr>
		    <td class="eventType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></td>
		    <td class="eventName"><a href="#event-{@anchor}"><code><xsl:value-of select="@leaf"/></code></a></td>
		    <td class="eventSummary">
		      <xsl:call-template name="format-summary">
			<xsl:with-param name="doc" select="doc"/>
		      </xsl:call-template>
		    </td>
		  </tr>
		</xsl:for-each>
	      </table>
	    </xsl:if>
	    <xsl:if test="members/method[constructor]">
	      <h2>Constructor Summary</h2>
	      <table class="methodSummaryTable">
		<tr>
		  <th class="methodFlagsHeader">Flags</th>
		  <th class="methodNameHeader">Name</th>
		  <th class="methodSummaryHeader">Summary</th>
		</tr>
		<xsl:for-each select="members/method[constructor]">
		  <xsl:call-template name="method-summary">
		    <xsl:with-param name="method" select="."/>
		  </xsl:call-template>
		</xsl:for-each>
	      </table>
	    </xsl:if>
	    <xsl:if test="members/method[returns and not(@specialname)]">
	      <h2>Method Summary</h2>
	      <table class="methodSummaryTable">
		<tr>
		  <xsl:if test="not(/typedef/@interface)">
		    <th class="methodFlagsHeader">Flags</th>
		  </xsl:if>
		  <th class="methodNameHeader">Name</th>
		  <th class="methodSummaryHeader">Summary</th>
		</tr>
		<xsl:for-each select="members/method[returns and not(@specialname)]">
		  <xsl:call-template name="method-summary">
		    <xsl:with-param name="method" select="."/>
		  </xsl:call-template>
		</xsl:for-each>
	      </table>
	    </xsl:if>

	    <xsl:if test="members/field[not(@specialname)]">
	      <h2>Field Detail</h2>
	      <xsl:for-each select="members/field[not(@specialname)]">
		<div class="fieldDetail">
		  <a name="field-{@anchor}"></a>
		  <h3>
		    <xsl:call-template name="item-flags"><xsl:with-param name="node" select="."/></xsl:call-template>
		    <xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="type"/></xsl:call-template>
		    <xsl:text> </xsl:text>
		    <xsl:value-of select="@leaf"/>
		  </h3>
		  <!--
		      <table class="fieldDetailTable">
		      <tr>
		      <th class="fieldTypeHeader">Field type</th>
		      <td class="fieldType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></td>
		      </tr>
		      </table>
		  -->
		  <div class="fieldDoc">
		    <xsl:call-template name="format-doc">
		      <xsl:with-param name="doc" select="doc"/>
		    </xsl:call-template>
		  </div>
		</div>
	      </xsl:for-each>
	    </xsl:if>
	    <xsl:if test="members/property">
	      <h2>Property Detail</h2>
	      <xsl:for-each select="members/property">
		<div class="propertyDetail">
		  <a name="property-{@anchor}"></a>
		  <h3>
		    <xsl:if test="not(/typedef/@interface)">
		      <xsl:call-template name="property-flags"><xsl:with-param name="node" select="."/></xsl:call-template>
		    </xsl:if>
		    <xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="type"/></xsl:call-template>
		    <xsl:text> </xsl:text>
		    <xsl:value-of select="@leaf"/>
		    <span class="propertyAccessibility"> (<xsl:if test="getter">r</xsl:if><xsl:if test="setter">w</xsl:if>)</span>
		  </h3>
		  <!--
		      <table class="propertyDetailTable">
		      <tr>
		      <th class="propertyTypeHeader">Property type</th>
		      <td class="propertyType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></td>
		      </tr>
		      </table>
		  -->
		  <div class="propertyDoc">
		    <xsl:call-template name="format-doc">
		      <xsl:with-param name="doc" select="doc"/>
		    </xsl:call-template>
		  </div>
		</div>
	      </xsl:for-each>
	    </xsl:if>
	    <xsl:if test="members/event">
	      <h2>Event Detail</h2>
	      <xsl:for-each select="members/event">
		<div class="eventDetail">
		  <a name="event-{@anchor}"></a>
		  <h3>
		    <xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="type"/></xsl:call-template>
		    <xsl:text> </xsl:text>
		    <xsl:value-of select="@leaf"/>
		  </h3>
		  <!--
		      <table class="eventDetailTable">
		      <tr>
		      <th class="eventTypeHeader">Event type</th>
		      <td class="eventType"><xsl:call-template name="typeref"><xsl:with-param name="typeref" select="type"/></xsl:call-template></td>
		      </tr>
		      </table>
		  -->
		  <div class="eventDoc">
		    <xsl:call-template name="format-doc">
		      <xsl:with-param name="doc" select="doc"/>
		    </xsl:call-template>
		  </div>
		</div>
	      </xsl:for-each>
	    </xsl:if>
	    <xsl:if test="members/method[constructor]">
	      <h2>Constructor Detail</h2>
	      <xsl:for-each select="members/method[constructor]">
		<xsl:call-template name="method-detail">
		  <xsl:with-param name="method" select="."/>
		</xsl:call-template>
	      </xsl:for-each>
	    </xsl:if>
	    <xsl:if test="members/method[returns and not(@specialname)]">
	      <h2>Method Detail</h2>
	      <xsl:for-each select="members/method[returns and not(@specialname)]">
		<xsl:call-template name="method-detail">
		  <xsl:with-param name="method" select="."/>
		</xsl:call-template>
	      </xsl:for-each>
	    </xsl:if>
	  </xsl:otherwise>
	</xsl:choose>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>
