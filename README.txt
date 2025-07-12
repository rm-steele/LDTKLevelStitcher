This tool takes as input an LDtk world file (.ldtk or .json) and a series of images
that LDtk can export for each level, and combines them into a single image. The image
is output as "map.png".

The tool can optionally scale the resulting image down before exporting, as well as
ordering the images based on their depth in the editor.
Warning: This program can probably use multiple gigabytes of RAM on very large maps.

--------------------

Usage: LDTKLevelStitcher [-qsfoO] <World file path> <PNG directory path>

Arguments:
    -h, --help: Prints out this help information and exits.
    -v, --version: Prints version information and exits.
    -q, --quiet: Do not output any info text that isn't requested by other switches.
    --verbose: Print extra information used for debugging.
    -s <x>, --scale <x>: Before final output, scale the image down to 1/x its size.
    -f <regex>, --filter <regex>: Only include levels matching the specified regex 
      string. regex101.com is good at helping build these if you aren't familiar.
    -o: Order rooms by depth, with rooms of a smaller depth value drawing over larger values.
    -O: Order rooms by depth, with rooms of a larger depth value drawing over smaller values.
    -- : Stop porcessing arguments. For if your file path begins with - for some reason.
    World file path: The path to the .ldtk file that the PNGs are from.
    PNG directory path: The path to the directory containing the PNG images,
    obtained by telling them to be exported in Project Settings > Extra Files.