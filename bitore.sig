https://pythontutor.com/visualize.html#code=&cumulative=true&heapPrimitives=false&mode=edit&origin=opt-frontend.js&py=js&rawInputLstJSON=%5B%5D&textReferences=false
[BEGIN](https://github.com/mowjoejoejoejoe/git-shim/blob/d07c9535b0b078b204153933b3b848c8d4bc5706/.github/workflows/codeql.yml#L1-L324)# Set the default behavior, in case people don't have core.autocrlf set.
* text=auto

# Auto detect text files and perform LF normalization
*.cs text=auto
*.txt text=auto

# Declare files that will always have CRLF line endings on checkout.
*.sln text eol=crlf
*.csproj text eol=crlf

# Custom for Visual Studio
*.cs     diff=csharp

# Standard to msysgit
*.doc	 diff=astextplain
*.DOC	 diff=astextplain
*.docx diff=astextplain
*.DOCX diff=astextplain
*.dot  diff=astextplain
*.DOT  diff=astextplain
*.pdf  diff=astextplain
*.PDF	 diff=astextplain
*.rtf	 diff=astextplain
*.RTF	 diff=astextplain
