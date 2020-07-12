export TEXINPUTS=tex//:

all: thesis.pdf abstract.pdf abstract-cs.pdf

# LaTeX must be run multiple times to get references right
thesis.pdf: thesis.tex $(wildcard *.tex) bibliography.bib thesis.xmpdata $(wildcard *md)
	python _build.py
	pdflatex $<
	-bibtex thesis
	pdflatex $<
	pdflatex $<

abstract.pdf: abstract.tex abstract.xmpdata
	pdflatex $<

abstract-cs.pdf: abstract-cs.tex abstract-cs.xmpdata
	pdflatex $<

clean:
	rm -f *.log *.dvi *.aux *.toc *.lof *.lot *.out *.bbl *.blg *.xmpi *.md.tex *.fls *.fdb_latexmk
	rm -f thesis.pdf abstract.pdf
