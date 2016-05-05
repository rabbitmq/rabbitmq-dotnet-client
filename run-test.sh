#! /bin/bash

mono .paket/paket.bootstrapper.exe
mono .paket/paket.exe restore
mono ./packages/FAKE/tools/FAKE.exe build.fsx Test
