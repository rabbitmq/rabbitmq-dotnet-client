from __future__ import nested_scopes

import RenderUtils
import Config
import os

info = {
    "friendly_name": "Sequence Diagram",
    "example_spacing": " NameOfSequence\n  ",
    "example_template": """# Define the objects
  object(O,"o:Toolkit");
  placeholder_object(P);
  step();

  # Message sequences
  active(O);
  step();
  active(O);
  message(O,O,"callbackLoop()");
  inactive(O);
  create_message(O,P,"p:Peer");
  message(O,P,"handleExpose()");
  active(P);
  return_message(P,O,"");
  inactive(P);
  destroy_message(O,P);
  inactive(O);

  # Complete the lifelines
  step();
  complete(O);
""",
    
    "summary": "Draws a sequence diagram, described using the <tt>pic</tt> programming language.",
    "details": """

    <p>A sequence diagram, using Brian Kernighan's <a
    href='http://en.wikipedia.org/wiki/Pic_%28software%29'>Pic</a>
    diagramming DSL, extended with macros for sequence diagrams from
    Diomidis Spinellis' <a
    href='http://www.spinellis.gr/sw/umlgraph/'>UMLGraph</a>
    package. See <a
    href='http://www.spinellis.gr/sw/umlgraph/doc/seq-intro.html'>the
    sequence.pic documentation</a> for syntax details. Note that the
    <code>NameOfSequence</code> needs to be unique for each
    sequence diagram on a page.</p>

    """
}

def runpipe(command, input):
    (child_stdin, child_stdout) = os.popen2(command)
    child_stdin.write(input)
    child_stdin.close()
    output = child_stdout.read()
    child_stdout.close()
    return output

def SublanguageHandler(args, doc, renderer):
    args = args.split(' ')
    name = args[0]

    input = '.PS'
    if len(args) > 1:
	input = input + ' ' + args[1]
    input = input + '\ncopy "./sublanguages/sequence.pic";\n' \
	+ doc.reconstruct_child_text().as_string() \
	+ '\n\n.PE\n'
    #input = runpipe('/usr/bin/pic2plot -T fig', input)
    #output = runpipe('/usr/bin/fig2dev -L png', input)
    output = runpipe('./sublanguages/sequence-helper.sh', input)

    cachepath = 'sequence/' + name + '.png'
    renderer.add(RenderUtils.media_cache(renderer,
					 cachepath,
					 '[Sequence diagram ' + name + ']',
					 'pyle_mediacache_image',
					 'image/png',
					 output))
