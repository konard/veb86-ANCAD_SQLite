from pathlib import Path
import struct
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile


def verify(archive_path):
    bundle = "ANCAD_SQLite.bundle/"
    binaries = (
        "AcadDevices.DbEditor.dll",
        "AcadDevices.Core.dll",
        "System.Data.SQLite.dll",
        "x64/SQLite.Interop.dll",
    )
    prefix = bundle + "Contents/Win64/"
    with ZipFile(archive_path) as archive:
        assert archive.testzip() is None
        assert {info.filename for info in archive.infolist() if not info.is_dir()} == {
            "INSTALL.md",
            bundle + "PackageContents.xml",
            *(prefix + binary for binary in binaries),
        }
        manifest = ET.fromstring(archive.read(bundle + "PackageContents.xml"))
        version = manifest.get("AppVersion")
        assert archive_path.name == f"ANCAD_SQLite-manual-install-v{version}.zip"
        assert manifest.find("./Components/RuntimeRequirements").attrib == {
            "OS": "Win64",
            "Platform": "AutoCAD",
            "SeriesMin": "R23.1",
            "SeriesMax": "R24.3",
        }
        entry = manifest.find("./Components/ComponentEntry")
        assert entry.get("ModuleName") == "./Contents/Win64/AcadDevices.DbEditor.dll"
        assert entry.get("LoadOnCommandInvocation") == "True"
        assert entry.find("./Commands/Command").attrib == {
            "Global": "DB_EDIT",
            "Local": "DB_EDIT",
        }
        for binary in binaries:
            if binary == "System.Data.SQLite.dll":
                continue  # The managed SQLite provider loads the separate x64 native library.
            image = archive.read(prefix + binary)
            pe_offset, = struct.unpack_from("<I", image, 0x3C)
            assert image[pe_offset:pe_offset + 4] == b"PE\0\0", binary
            assert struct.unpack_from("<H", image, pe_offset + 4)[0] == 0x8664, binary

    print(f"Verified {archive_path.name}")


if __name__ == "__main__":
    verify(Path(sys.argv[1]))
