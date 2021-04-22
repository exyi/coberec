export TEXINPUTS=tex//:

all: abstract-cs-core.tex abstract-core.tex IL_rewriting_diagram.pdf thesis.pdf abstract.pdf abstract-cs.pdf
.PHONY: abstract-cs.pdf abstract.pdf

# LaTeX must be run multiple times to get references right
thesis.pdf: thesis.tex $(wildcard *.tex) bibliography.bib thesis.xmpdata $(wildcard *md)
	python _build.py
	pdflatex $<
	-bibtex thesis
	pdflatex $<
	pdflatex $<

IL_rewriting_diagram.pdf:
	inkscape -D IL_rewriting_diagram.svg -o IL_rewriting_diagram.pdf

abstract-core.tex:
	pandoc abstract.md -o abstract-core.tex

abstract-cs-core.tex:
	pandoc abstract-cs.md -o abstract-cs-core.tex

abstract.pdf: abstract.tex abstract.xmpdata
	pdflatex $<


abstract-cs.pdf: abstract-cs.tex abstract-cs.xmpdata
	pdflatex $<

clean:
	rm -f *.log *.dvi *.aux *.toc *.lof *.lot *.out *.bbl *.blg *.xmpi *.md.tex *.fls *.fdb_latexmk
	rm -f thesis.pdf abstract.pdf
	rm abstract-core.tex abstract-cs-core.tex IL_rewriting_diagram.pdf IL_rewriting_diagram.pdf_tex
