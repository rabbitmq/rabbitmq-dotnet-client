#!/usr/bin/perl -w
use strict;
use diagnostics;
use File::Spec;
use File::Basename;

###########################################################################

if (scalar(@ARGV) < 3) {
    print STDERR "Usage: migrate-pyle1.pl <sourcedir> <targetdir> <attachmentdir>\n";
    exit 1;
}

my ($sourcedir, $targetdir, $attachmentdir) = @ARGV;

###########################################################################

my %SUFFIXMAP = (
                 ".html" => "text/html",
                 ".txt" => "text/plain",
                 ".text" => "text/plain",
                 ".gif" => "image/gif",
                 ".jpg" => "image/jpeg",
                 ".jpeg" => "image/jpeg",
                 ".png" => "image/png",
                 ".rtf" => "application/rtf",
                 ".pdf" => "application/pdf",
                 ".doc" => "application/msword",
                 ".mpp" => "application/vnd.ms-project",
                 ".gz" => "application/x-gzip",
                 ".tgz" => "application/x-gzipped-tar",
                 ".zip" => "application/x-zip-compressed",
                 ".wav" => "audio/wav",
                 );

my %FILEREGEXES = (
                   "JPEG image data" => "image/jpeg",
                   "PNG image data" => "image/png",
                   "Microsoft Office Document" => "application/msword",
                   "PDF document" => "application/pdf",
                   "gzip compressed data" => "application/x-gzip",
                   "Zip archive data" => "application/x-zip-compressed",
                   "text" => "text/plain",
                   );

sub guess_mimetype {
    my ($filename) = @_;
    my ($name,$path,$suffix) = fileparse($filename, qr/\.[^.]*/);
    #print "Seen $name, $path, $suffix\n";
    my $mimetype = $SUFFIXMAP{lc $suffix} || "";
    #print " ".(lc $suffix)." ---> $mimetype\n";
    if ($mimetype) {
        return $mimetype;
    } else {
        my $fileoutput = `file "$attachmentdir/$filename"`;
        for my $regex (keys %FILEREGEXES) {
            if ($fileoutput =~ /$regex/) {
                return $FILEREGEXES{$regex};
            }
        }
    }
}

###########################################################################

print "Migrating $sourcedir to $targetdir...\n";
system "mkdir", "-p", $targetdir;
system "mkdir", "-p", $attachmentdir;

foreach my $datapath (glob("$sourcedir/*.txt")) {
    next if $datapath =~ /\.attach\./;
    my ($datavol, $datadir, $datafile) = File::Spec->splitpath($datapath);
    my $newdatafile = "data.$datafile";
    print "Copying page $datapath -> $newdatafile\n";
    system "cp", $datapath, "$targetdir/$newdatafile";
}

my @unknown_attachments = ();

foreach my $attachpath (glob("$sourcedir/*.attach.*")) {
    chomp $attachpath;
    my ($attachvol, $attachdir, $attachfile) = File::Spec->splitpath($attachpath);

    if ($attachfile =~ /\*\*[a-z]+\*/) {
        print "Skipping $attachfile.\n";
    } else {
        $attachfile =~ /^([^.]+)\.attach\.(.+)\.txt$/;
        my $newattachfile = "data.$1.attach.$2";
        my $metafile = "meta.$1.attach.$2";
        print "Copying attachment $attachfile -> $newattachfile\n";
        system "cp", $attachpath, "$attachmentdir/$newattachfile";

        my $attachsize = ((stat($attachpath))[7]);
        my $mimetype = guess_mimetype($newattachfile);
        #print "Mime: $newattachfile == $mimetype\n";
        if ($mimetype) {
            open META, "> $attachmentdir/$metafile" || die $!;
            print META "mimetype: $mimetype\n";
            print META "bodylen: $attachsize\n";
            close META;
        } else {
            push @unknown_attachments, $newattachfile;
        }
    }
}

foreach my $unknown_attachment (@unknown_attachments) {
    print "UNKNOWN ATTACHMENT TYPE: $unknown_attachment - check mime type manually\n";
}
