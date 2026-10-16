#!/usr/bin/env python3
"""Check legal notices in built NuGet packages and, optionally, a demo output."""
import argparse
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def verify(packages_dir, demo_output=None):
    packages = sorted(packages_dir.glob('KD.Avalonia.Rice.*.nupkg'))
    if not packages:
        raise ValueError(f'No library NuGet packages found in {packages_dir}')
    required = [ROOT / 'LICENSE', ROOT / 'THIRD_PARTY_NOTICES.md',
                *sorted((ROOT / 'licenses').glob('*.txt'))]
    for package in packages:
        with zipfile.ZipFile(package) as archive:
            manifests = [name for name in archive.namelist() if name.endswith('.nuspec')]
            if len(manifests) != 1:
                raise ValueError(f'{package.name}: expected exactly one NuGet manifest')
            manifest = ET.fromstring(archive.read(manifests[0]))
            license_node = manifest.find('.//{*}license')
            if license_node is None or license_node.get('type') != 'expression' or license_node.text != 'MIT':
                raise ValueError(f'{package.name}: missing MIT license expression')
            for source in required:
                name = source.relative_to(ROOT).as_posix()
                if archive.read(name) != source.read_bytes():
                    raise ValueError(f'{package.name}: missing or stale {name}')
            if archive.read('icon.png') != (ROOT / 'src/KD.Avalonia.Rice/Assets/icon.png').read_bytes():
                raise ValueError(f'{package.name}: incorrect package icon')
            forbidden = ('TestData', '-examples.json', 'CC-BY-SA', 'licenses/demo/')
            if any(part in name for name in archive.namelist() for part in forbidden):
                raise ValueError(f'{package.name}: test data or demo-only licenses leaked into the library package')
        print(f'OK: {package.name}: MIT metadata, own license and third-party texts')
    if demo_output:
        demo_required = [ROOT / 'LICENSE', ROOT / 'THIRD_PARTY_NOTICES.md',
                         *sorted((ROOT / 'licenses').rglob('*.txt'))]
        for source in demo_required:
            target = demo_output / source.relative_to(ROOT)
            if not target.is_file() or target.read_bytes() != source.read_bytes():
                raise ValueError(f'Demo output: missing or stale {source.relative_to(ROOT)}')
        print(f'OK: demo output: {len(demo_required)} license/notice files')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages-dir', type=Path, default=ROOT / 'artifacts/packages')
    parser.add_argument('--demo-output', type=Path)
    args = parser.parse_args()
    try:
        verify(args.packages_dir, args.demo_output)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, ET.ParseError) as error:
        print(f'License packaging check failed: {error}', file=sys.stderr)
        sys.exit(1)
