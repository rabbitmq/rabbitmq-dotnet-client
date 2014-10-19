import Core
import Block
import re

info = {
    "friendly_name": "Table",
    "example_spacing": "  (1,3-4,colspan='2') (2,1,style='background: #eee')\n  ",
    "example_template": """Top left. || Top right.
  More in the top left cell.
  || More in the top right cell.

  Second row. || Bottom right.
              || MarkupSyntax is enabled in tables.

  Third row, with colspan="2".
  Third row, with colspan="2".
  Third row, with colspan="2".
  Third row, with colspan="2".
  Third row, with colspan="2".

  @dot TestTable
   digraph TestTable {
     x -> y -> x;
   }
""",

    "summary": "Inserts a table in the output.",
    "details": """
    
    <p>Tables are introduced using the <code>@table</code>
    sublanguage. Columns are separated using '<code>||</code>', and
    rows are separated by a blank line. Since there's no way at
    present to escape the column separators, it's not possible to have
    tables within tables, and it can be problematic trying to include
    code or non-markup syntax using '<code>||</code>' within a table
    cell.</p>

    <h3>BNF syntax definition for tables.</h3>

    <pre>table = '@table' attribute-spec* NEWLINE child-paragraph*

attribute-spec = '(' range ',' range ',' html-attributes ')'

range = DIGIT+
      | DIGIT+ '-'
      | '-' DIGIT+
      | DIGIT+ '-' DIGIT+</pre>
      
      <p>You can supply HTML attributes for ranges of cells in your
      table by supplying one or more <code>attribute-spec</code>s in
      the <code>@table</code> header. Each <code>attribute-spec</code>
      is a triple of a column range, a row range, and some HTML
      attributes, in that order. For example, the table header</p>

      <pre>@table (1,1,style='font-weight: bold') (2-3,-,class='myCSSclass')</pre>
      
      <p>causes the top-left cell to have an HTML <code>style</code>
      attribute, and all cells in columns 2 through 3 to have an HTML
      <code>class</code> attribute. Multiple HTML attributes can be
      separated by spaces, as they would appear in regular HTML
      documents.</p>

      <p>Ranges can be:</p>

      <ul>
        <li><code>NN</code> - a single (1-based) column or row</li>
        <li><code>NN-MM</code> - an inclusive 1-based range of columns or rows</li>
        <li><code>-MM</code> - an inclusive range of columns or rows up to a particular column or row</li>
        <li><code>NN-</code> - an inclusive range of columns or rows starting from a particular column or row</li>
      </ul>

      """
}

colspecre = re.compile(r"\s*\(([^)]+)\)\s*")

def lenient_int(s):
    try:
        return int(s)
    except:
        return None

def parse_colspan(spec):
    bits = map(lenient_int, spec.split('-'))
    if len(bits) == 1:
        return (bits[0], bits[0])
    else:
        return (bits[0], bits[1])

def collect_colspecs(args):
    acc = []
    prefix = ''
    haveFirstMatch = 0
    while 1:
        match = colspecre.search(args)
        if match:
            (xspec, yspec, attrs) = match.group(1).split(',', 2)
            acc.append((parse_colspan(xspec), parse_colspan(yspec), attrs))
            if not haveFirstMatch:
                haveFirstMatch = 1
                prefix = args[:match.start()].strip()
            args = args[match.end():]
        else:
            break
    return (acc, prefix)

def in_range(c, spec):
    (lo, hi) = spec
    if lo == None:
        if hi == None:
            return 1
        else:
            return c <= hi
    else:
        if hi == None:
            return c >= lo
        else:
            return (c >= lo) and (c <= hi)

def attrs_for(x, y, colspecs):
    acc = [attrs
           for (xspec, yspec, attrs) in colspecs
           if in_range(x, xspec) and in_range(y, yspec)]
    return ' '.join(acc)

def finish_row(rows, subparas):
    if subparas:
        rows.append(map(Block.parselines, subparas))
    return []

def group_columns(para):
    rows = []
    subparas = []
    for line in para.lines:
        if line.strip() == '':
            subparas = finish_row(rows, subparas)
        else:
            sublines = line.split('||')
            while len(subparas) < len(sublines):
                subparas.append([])
            index = 0
            for subline in sublines:
                subparas[index].append(subline.rstrip())
                index = index + 1
    subparas = finish_row(rows, subparas)
    return rows

class TableCell(Core.Container):
    def __init__(self, colnum, rownum, attrs):
	Core.Container.__init__(self)
	self.colnum = colnum
	self.rownum = rownum
	self.attrs = attrs

    def templateName(self):
	return 'pyle_tablecell'

class TableRow(Core.Container):
    def __init__(self, rownum):
	if rownum % 2:
	    rowclass = 'oddrow'
	else:
	    rowclass = 'evenrow'
	Core.Container.__init__(self, rowclass)
	self.rownum = rownum

    def templateName(self):
	return 'pyle_tablerow'

class Table(Core.Container):
    def __init__(self, prefix):
	Core.Container.__init__(self)
	self.prefix = prefix
	self.columncount = 0

    def templateName(self):
	return 'pyle_table'

def SublanguageHandler(args, doc, renderer):
    (colspecs, prefix) = collect_colspecs(args)
    rows = group_columns(doc.reconstruct_child_text())

    if not prefix:
        prefix = 'class="wikimarkup"'

    table = Table(prefix)
    renderer.push_acc(table)

    rownum = 0
    for cols in rows:
        rownum = rownum + 1
	renderer.push_acc(TableRow(rownum))

        colnum = 0
        for celldoc in cols:
            colnum = colnum + 1
	    renderer.push_visit_pop(TableCell(colnum, rownum, attrs_for(colnum, rownum, colspecs)),
				    celldoc.children)
            table.columncount = max(table.columncount, colnum)
	renderer.pop_acc()
    renderer.pop_acc()
