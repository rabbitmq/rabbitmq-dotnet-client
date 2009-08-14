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
  <xsl:template name="namespace-printname">
    <xsl:param name="namespace-name" select="@name"/>
    <xsl:choose>
      <xsl:when test="$namespace-name != ''"><xsl:value-of select="$namespace-name"/></xsl:when>
      <xsl:otherwise>(empty)</xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="type-alias-impl">
    <xsl:param name="name"/>
    <xsl:param name="leaf"/>
    <xsl:param name="prefix" select="''"/>
    <xsl:param name="declaringtype"/>
    <xsl:param name="genericarguments"/>
    <xsl:param name="referenceChain"/>

    <xsl:choose>
      <xsl:when test="substring($referenceChain, 1, 1) = 'A'">
	<xsl:call-template name="type-alias-impl">
	  <xsl:with-param name="name" select="$name"/>
	  <xsl:with-param name="leaf" select="$leaf"/>
	  <xsl:with-param name="prefix" select="$prefix"/>
	  <xsl:with-param name="declaringtype" select="$declaringtype"/>
	  <xsl:with-param name="genericarguments" select="$genericarguments"/>
	  <xsl:with-param name="referenceChain" select="substring($referenceChain, 2)"/>
	</xsl:call-template>
	<xsl:text>[]</xsl:text>
      </xsl:when>
      <xsl:when test="substring($referenceChain, 1, 1) = 'R'">
	<xsl:call-template name="type-alias-impl">
	  <xsl:with-param name="name" select="$name"/>
	  <xsl:with-param name="leaf" select="$leaf"/>
	  <xsl:with-param name="prefix" select="$prefix"/>
	  <xsl:with-param name="declaringtype" select="$declaringtype"/>
	  <xsl:with-param name="genericarguments" select="$genericarguments"/>
	  <xsl:with-param name="referenceChain" select="substring($referenceChain, 2)"/>
	</xsl:call-template>
	<xsl:text>&amp;</xsl:text>
      </xsl:when>
      <xsl:when test="$referenceChain = ''">
	<xsl:value-of select="$prefix"/>
	<xsl:if test="$declaringtype/type">
	  <xsl:call-template name="type-alias">
	    <xsl:with-param name="typeref" select="$declaringtype/type"/>
	  </xsl:call-template>
	  <xsl:text>.</xsl:text>
	</xsl:if>
	<xsl:choose>
	  <xsl:when test="$name = 'System.Void'">void</xsl:when>
	  <xsl:when test="$name = 'System.Object'">object</xsl:when>
	  <xsl:when test="$name = 'System.String'">string</xsl:when>
	  <xsl:when test="$name = 'System.Byte'">byte</xsl:when>
	  <xsl:when test="$name = 'System.Char'">char</xsl:when>
	  <xsl:when test="$name = 'System.Int16'">short</xsl:when>
	  <xsl:when test="$name = 'System.Int32'">int</xsl:when>
	  <xsl:when test="$name = 'System.Int64'">long</xsl:when>
	  <xsl:when test="$name = 'System.UInt16'">ushort</xsl:when>
	  <xsl:when test="$name = 'System.UInt32'">uint</xsl:when>
	  <xsl:when test="$name = 'System.UInt64'">ulong</xsl:when>
	  <xsl:when test="$name = 'System.Boolean'">bool</xsl:when>
	  <xsl:when test="$name = 'System.Double'">double</xsl:when>
	  <xsl:when test="$name = 'System.Single'">single</xsl:when>
	  <xsl:otherwise><xsl:value-of select="$leaf"/></xsl:otherwise>
	</xsl:choose>
	<xsl:call-template name="emit-generic-arguments">
	  <xsl:with-param name="genericarguments" select="$genericarguments"/>
	</xsl:call-template>
      </xsl:when>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="emit-generic-arguments">
    <xsl:param name="genericarguments"/>
    <xsl:if test="$genericarguments">&lt;<xsl:for-each select="$genericarguments/type">
    <xsl:call-template name="type-alias">
      <xsl:with-param name="typeref" select="."/>
    </xsl:call-template><xsl:if test="position() != last()">,</xsl:if>
    </xsl:for-each>&gt;</xsl:if>
  </xsl:template>

  <xsl:template name="type-alias">
    <xsl:param name="typeref"/>
    <xsl:param name="declaringtype" select="$typeref/declaringtype"/>
    <xsl:call-template name="type-alias-impl">
      <xsl:with-param name="name" select="$typeref/@name"/>
      <xsl:with-param name="leaf" select="$typeref/@leaf"/>
      <xsl:with-param name="declaringtype" select="$declaringtype"/>
      <xsl:with-param name="genericarguments" select="$typeref/genericarguments"/>
      <xsl:with-param name="referenceChain" select="$typeref/@referenceChain"/>
    </xsl:call-template>
  </xsl:template>

  <xsl:template name="item-flags">
    <xsl:param name="node"/>
    <xsl:if test="$node/@assembly">internal </xsl:if>
    <xsl:if test="$node/@private">private </xsl:if>
    <xsl:if test="$node/@public">public </xsl:if>
    <xsl:if test="$node/@family">protected </xsl:if>
    <xsl:if test="$node/@visible"></xsl:if>

    <xsl:if test="$node/@abstract and not($node/@interface)">abstract </xsl:if>
    <xsl:if test="$node/@constructor"></xsl:if>
    <xsl:if test="$node/@virtual">virtual </xsl:if>
    <xsl:if test="$node/@final">final </xsl:if>
    <xsl:if test="$node/@initonly">initonly </xsl:if>
    <xsl:if test="$node/@specialname"></xsl:if>
    <xsl:choose>
      <xsl:when test="$node/@literal and $node/@static">const </xsl:when>
      <xsl:when test="$node/@literal">literal </xsl:when>
      <xsl:when test="$node/@static">static </xsl:when>
      <xsl:otherwise />
    </xsl:choose>

    <xsl:if test="$node/@class">
      <xsl:choose>
        <xsl:when test="$node/extends/class/type[@name = 'System.MulticastDelegate' or
                                                 @name = 'System.Delegate']">
          <xsl:text>delegate </xsl:text>
	</xsl:when>
	<xsl:otherwise>
	  <xsl:text>class </xsl:text>
	</xsl:otherwise>
      </xsl:choose>
    </xsl:if>
    <xsl:if test="$node/@enum">enum </xsl:if>
    <xsl:if test="$node/@interface">interface </xsl:if>
    <xsl:if test="$node/@valuetype">struct </xsl:if>
  </xsl:template>

  <xsl:template name="property-flags">
    <xsl:param name="node"/>
    <xsl:choose>
      <xsl:when test="$node/getter">
	<xsl:call-template name="item-flags">
	  <xsl:with-param name="node" select="$node/getter"/>
	</xsl:call-template>
      </xsl:when>
      <xsl:when test="$node/setter">
	<xsl:call-template name="item-flags">
	  <xsl:with-param name="node" select="$node/setter"/>
	</xsl:call-template>
      </xsl:when>
      <xsl:otherwise />
    </xsl:choose>
  </xsl:template>

  <xsl:template name="typeref">
    <xsl:param name="fully-qualified"/>
    <xsl:param name="typeref"/>
    <xsl:param name="declaringtype" select="$typeref/declaringtype"/>
    <xsl:call-template name="typeref-impl">
      <xsl:with-param name="local" select="$typeref/@local"/>
      <xsl:with-param name="name" select="$typeref/@name"/>
      <xsl:with-param name="leaf" select="$typeref/@leaf"/>
      <xsl:with-param name="prefix">
	<xsl:choose>
	  <xsl:when test="$fully-qualified">
	    <xsl:if test="$typeref/@namespace != ''">
	      <xsl:value-of select="concat($typeref/@namespace, '.')"/>
	    </xsl:if>
	  </xsl:when>
	  <xsl:otherwise>
	    <xsl:text></xsl:text>
	  </xsl:otherwise>
	</xsl:choose>
      </xsl:with-param>
      <xsl:with-param name="declaringtype" select="$declaringtype"/>
      <xsl:with-param name="genericarguments" select="$typeref/genericarguments"/>
      <xsl:with-param name="referenceChain" select="$typeref/@referenceChain"/>
    </xsl:call-template>
  </xsl:template>

  <xsl:template name="typeref-impl">
    <xsl:param name="local"/>
    <xsl:param name="name"/>
    <xsl:param name="leaf"/>
    <xsl:param name="prefix" select="''"/>
    <xsl:param name="declaringtype"/>
    <xsl:param name="genericarguments"/>
    <xsl:param name="referenceChain"/>
    <xsl:param name="suffix" select="''"/>
    <xsl:choose>
      <xsl:when test="$local = 'true'">
	    <code>
	      <a class="localTypeLink" title="{$name}" href="type-{$name}.html">
	        <xsl:call-template name="type-alias-impl">
	          <xsl:with-param name="name" select="$name"/>
	          <xsl:with-param name="leaf" select="$leaf"/>
	          <xsl:with-param name="prefix" select="$prefix"/>
	          <xsl:with-param name="declaringtype" select="$declaringtype"/>
	          <xsl:with-param name="genericarguments" select="$genericarguments"/>
	          <xsl:with-param name="referenceChain" select="$referenceChain"/>
	        </xsl:call-template>
	      </a>
	      <xsl:value-of select="$suffix"/>
	    </code>
      </xsl:when>
      <xsl:when test="$local = 'genericparameter'">
	    <code>
	      <span class="genericParameterTypeRef" title="{$name}">
	        <xsl:call-template name="type-alias-impl">
	          <xsl:with-param name="name" select="$name"/>
	          <xsl:with-param name="leaf" select="$leaf"/>
	          <xsl:with-param name="prefix" select="$prefix"/>
	          <xsl:with-param name="declaringtype" select="$declaringtype"/>
	          <xsl:with-param name="genericarguments" select="$genericarguments"/>
	          <xsl:with-param name="referenceChain" select="$referenceChain"/>
	        </xsl:call-template>
	      </span>
	      <xsl:value-of select="$suffix"/>
	    </code>
      </xsl:when>
      <xsl:otherwise>
	    <code>
	      <xsl:variable name="queryparam">
	        <xsl:choose>
	          <xsl:when test="substring-before($name, '`')"><xsl:value-of select="substring-before($name, '`')"/>+generic</xsl:when>
	          <xsl:otherwise><xsl:value-of select="$name"/></xsl:otherwise>
	        </xsl:choose>
	      </xsl:variable>
          <xsl:choose>
            <xsl:when test="$googleNonlocalTypes = 'true'">
              <code>
	            <a class="nonlocalTypeLink" title="{$name}" href="http://www.google.com/search?q={$queryparam}&amp;btnI=I'm Feeling Lucky">
	              <xsl:call-template name="type-alias-impl">
	                <xsl:with-param name="name" select="$name"/>
	                <xsl:with-param name="leaf" select="$leaf"/>
	                <xsl:with-param name="prefix" select="$prefix"/>
	                <xsl:with-param name="declaringtype" select="$declaringtype"/>
	                <xsl:with-param name="genericarguments" select="$genericarguments"/>
	                <xsl:with-param name="referenceChain" select="$referenceChain"/>
	              </xsl:call-template>
	            </a>
              </code>
            </xsl:when>
            <xsl:otherwise>
	          <code>
	            <span class="nonlocalTypeLink" title="{$name}">
	              <xsl:call-template name="type-alias-impl">
	                <xsl:with-param name="name" select="$name"/>
	                <xsl:with-param name="leaf" select="$leaf"/>
	                <xsl:with-param name="prefix" select="$prefix"/>
	                <xsl:with-param name="declaringtype" select="$declaringtype"/>
	                <xsl:with-param name="genericarguments" select="$genericarguments"/>
	                <xsl:with-param name="referenceChain" select="$referenceChain"/>
	              </xsl:call-template>
	            </span>
              </code>
            </xsl:otherwise>
          </xsl:choose>
	      <xsl:value-of select="$suffix"/>
	    </code>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="format-doc">
    <xsl:param name="doc"/>
    <xsl:choose>
      <xsl:when test="$doc/*">
	<xsl:apply-templates mode="doc-toplevel" select="$doc/*[local-name() != 'see']"/>
	<xsl:if test="$doc/see">
	  <div class="fullDoc seeAlso">
	    <h4>See</h4>
	    <div class="fullDocBody">
	      <ul>
		<xsl:for-each select="$doc/see[substring(@cref,1,2) = 'T:']">
		  <li>
		    <xsl:call-template name="typeref-impl">
		      <!-- There's not enough information in the input to set local automatically. -->
		      <xsl:with-param name="local" select="'true'"/>
		      <xsl:with-param name="name" select="substring(@cref,3)"/>
		      <xsl:with-param name="leaf" select="substring(@cref,3)"/>
		      <xsl:with-param name="declaringtype" select="/.."/>
		      <xsl:with-param name="genericarguments" select="/.."/>
		      <xsl:with-param name="referenceChain" select="''"/>
		    </xsl:call-template>
		  </li>
		</xsl:for-each>
	      </ul>
	    </div>
	  </div>
	</xsl:if>
      </xsl:when>
      <xsl:otherwise>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template mode="doc-toplevel" match="*">
    <div class="fullDoc">
      <h4>
	<xsl:value-of select="concat(translate(substring(local-name(),1,1),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ'),substring(local-name(),2))"/>
      </h4>
      <div class="fullDocBody"><xsl:apply-templates mode="doc-body" select="node()"/></div>
    </div>
  </xsl:template>

  <xsl:template mode="doc-body" match="@*">
    <xsl:copy/>
  </xsl:template>

  <xsl:template mode="doc-body" match="para">
    <p><xsl:apply-templates mode="doc-body" select="@*|node()"/></p>
  </xsl:template>

  <xsl:template mode="doc-body" match="example">
    <div class="example"><xsl:apply-templates mode="doc-body" select="@*|node()"/></div>
  </xsl:template>

  <xsl:template mode="doc-body" match="para/code">
    <code><xsl:apply-templates mode="doc-body" select="@*|node()"/></code>
  </xsl:template>

  <xsl:template mode="doc-body" match="code">
    <pre class="code"><xsl:apply-templates mode="doc-body" select="@*|node()"/></pre>
  </xsl:template>

  <xsl:template mode="doc-body" match="*">
    <xsl:copy><xsl:apply-templates mode="doc-body" select="@*|node()"/></xsl:copy>
  </xsl:template>

  <xsl:template name="format-summary">
    <xsl:param name="doc"/>
    <xsl:choose>
      <xsl:when test="$doc/summary">
	<p class="docSummary documented"><xsl:copy-of select="$doc/summary/node()"/></p>
      </xsl:when>
      <xsl:otherwise>
	<p class="docSummary undocumented">(undocumented)</p>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="parameter-type">
    <xsl:param name="parameter"/>
    <xsl:param name="isref" select="'false'"/>
    <span>
      <xsl:attribute name="class">parameterType<xsl:if test="$parameter/@output = 'true'"> parameterDirectionOut</xsl:if><xsl:if test="$parameter/@reference = 'true'"> parameterDirectionRef</xsl:if></xsl:attribute>
      <xsl:choose>
	<xsl:when test="$isref = 'true'">
	  <xsl:if test="$parameter/@output = 'true'"><code>out </code></xsl:if>
	  <xsl:if test="$parameter/@reference = 'true'"><code>ref </code></xsl:if>
	  <xsl:call-template name="typeref"><xsl:with-param name="typeref" select="$parameter/type"/></xsl:call-template>
	</xsl:when>
	<xsl:otherwise>
	  <xsl:if test="$parameter/@output = 'true'">out </xsl:if>
	  <xsl:if test="$parameter/@reference = 'true'">ref </xsl:if>
	  <xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="$parameter/type"/></xsl:call-template>
	</xsl:otherwise>
      </xsl:choose>
    </span>
  </xsl:template>

  <xsl:template name="method-signature-parameters">
    <xsl:param name="method"/>(<xsl:for-each select="$method/parameters/parameter"><xsl:call-template name="parameter-type"><xsl:with-param name="parameter" select="."/></xsl:call-template><xsl:text> </xsl:text><xsl:value-of select="@name"/><xsl:if test="position() != last()">, </xsl:if></xsl:for-each>)
  </xsl:template>

  <xsl:template name="method-signature">
    <xsl:param name="method"/>
    <xsl:param name="methodname" select="$method/@leaf"/>
    <code>
      <xsl:if test="$method/returns">
	<xsl:call-template name="type-alias"><xsl:with-param name="typeref" select="$method/returns/type"/></xsl:call-template><xsl:text> </xsl:text>
      </xsl:if>
      <xsl:value-of select="$methodname"/><xsl:call-template name="emit-generic-arguments">
        <xsl:with-param name="genericarguments" select="$method/genericarguments"/>
      </xsl:call-template>
      <xsl:call-template name="method-signature-parameters">
	<xsl:with-param name="method" select="$method"/>
      </xsl:call-template>
    </code>
  </xsl:template>
</xsl:stylesheet>
