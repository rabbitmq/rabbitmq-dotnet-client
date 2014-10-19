#!/bin/sh
export PATH=/sw/bin:$PATH
pic2plot -T fig | fig2dev -L png
