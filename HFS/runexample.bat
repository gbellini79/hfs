hfs -i examples\%1 -o - | ffmpeg -i - -crf 17 -preset slow -shortest %2 -y