import Config
import Inline
import Core
import os
import re

def get_enscript_languages():
    if not Config.code_enscript_command:
        return "(none - Config.code_enscript_command is not set)"

    file = os.popen(Config.code_enscript_command + ' --help-highlight | grep ^Name:')
    names = [line[5:].strip() for line in file.readlines()]
    file.close()
    return ', '.join(names)

info = {
    "friendly_name": "Source Code (Block)",
    "example_spacing": " languagecode\n  ",
    "example_template": "int main(int argc, char const **argv) {\n    ...\n  }",
    "summary": "Prints the contained text in a source-code typeface, possibly with syntax-highlighting, without interpreting it as markup.",
    "details": """

    <p>If 'enscript' is installed (and configured in Config.py!), and a
    language code is supplied as an argument to the '@code' line, it
    will be used to provide syntax highlighting for the named
    language. All the languages supported by the installed version of
    enscript can be used. At the time this server was started, the
    list was:</p>

    <p>%s</p>

    """ % get_enscript_languages()
}

import warnings
import exceptions
warnings.filterwarnings('ignore',
                        r'.*tmpnam is a potential security risk to your program$',
                        exceptions.RuntimeWarning,
                        r'.*sublanguages\.code$',
                        49)

enscriptre = re.compile('.*<PRE>(.*)</PRE>.*', re.S)

def SublanguageHandler(args, doc, renderer):
    code = doc.reconstruct_child_text().as_string()
    literalfragment = Inline.LiteralFragment(code)
    if Config.code_enscript_command:
	filename = os.tmpnam()
	file = open(filename, 'w+')
	file.write(code)
	file.close()
	command = Config.code_enscript_command + ' -B -p - --language=html --color -E' + \
	    args + ' ' + filename + ' 2>/dev/null'
	child_stdout = os.popen(command)
	result = child_stdout.read()
	child_stdout.close()
	os.unlink(filename)
        renderer.add(HighlightedCode(Inline.HtmlFragment(enscriptre.sub(r'\1', result)),
                                     literalfragment))
    else:
	renderer.add(HighlightedCode(literalfragment, literalfragment))

class HighlightedCode(Core.Renderable):
    def __init__(self, highlightedFragment, plainFragment):
        self.highlightedFragment = highlightedFragment
        self.plainFragment = plainFragment

    def templateName(self):
        return 'pyle_highlightedcode'
