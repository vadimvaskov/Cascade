#!/usr/bin/env python3
"""Verify a Cascade device archive without exporting credentials or modifying it."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('archive', type=Path)
parser.add_argument('--version', required=True)
parser.add_argument('--build', required=True)
parser.add_argument('--report', type=Path, required=True)
parser.add_argument('--allow-missing-framework-dsym', action='append', default=[],
                    help='Explicitly record a missing vendor dSYM as a warning; never synthesize symbols.')
args = parser.parse_args()

def run(*command):
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(f'{command[0]} failed: {result.stderr.strip()}')
    return result.stdout.strip()

def require(value, message):
    if not value:
        raise RuntimeError(message)

apps = list((args.archive / 'Products/Applications').glob('*.app'))
require(len(apps) == 1, 'Expected one archived app')
app = apps[0]
info = plistlib.loads((app / 'Info.plist').read_bytes())
require(info['CFBundleIdentifier'] == 'fitness.quokka.caskade', 'Wrong Bundle ID')
require(info['CFBundleShortVersionString'] == args.version, 'Wrong release version')
require(info['CFBundleVersion'] == args.build, 'Wrong build number')
require(info['CFBundleSupportedPlatforms'] == ['iPhoneOS'], 'Not an iPhone device archive')
require(info.get('ITSAppUsesNonExemptEncryption') is False, 'Encryption declaration missing')
require(info.get('UIRequiresFullScreen') is True, 'Full-screen setting missing')
require(info.get('UIDeviceFamily') == [1, 2], 'Expected iPhone and iPad support')
require(bool(info.get('CFBundleIcons')), 'App icon metadata missing')
executable = app / info['CFBundleExecutable']
require(run('lipo', '-archs', str(executable)) == 'arm64', 'Expected arm64 executable')
run('codesign', '--verify', '--deep', '--strict', str(app))
profile = plistlib.loads(run('security', 'cms', '-D', '-i', str(app / 'embedded.mobileprovision')).encode())
require(profile['TeamIdentifier'] == ['RUT46C38YA'], 'Wrong provisioning team')
require(profile['Entitlements']['application-identifier'] == 'RUT46C38YA.fitness.quokka.caskade', 'Wrong profile app')
manifests = list(app.rglob('PrivacyInfo.xcprivacy'))
require(bool(manifests), 'No privacy manifest in archive')
privacy = []
for manifest in manifests:
    data = plistlib.loads(manifest.read_bytes())
    privacy.append({'path': str(manifest.relative_to(app)), 'accessedAPIs': data.get('NSPrivacyAccessedAPITypes', [])})
dsym = args.archive / 'dSYMs' / (app.name + '.dSYM')
require(dsym.exists(), 'Application dSYM missing')
uuid = run('dwarfdump', '--uuid', str(executable)).split()[1]
require(uuid in run('dwarfdump', '--uuid', str(dsym)), 'Application dSYM UUID mismatch')
framework_symbols = []
warnings = []
for framework in (app / 'Frameworks').glob('*.framework'):
    framework_info = plistlib.loads((framework / 'Info.plist').read_bytes())
    binary = framework / framework_info['CFBundleExecutable']
    framework_uuid = run('dwarfdump', '--uuid', str(binary))
    if not framework_uuid:
        continue  # Static archives have no independent linked-image UUID.
    symbols = args.archive / 'dSYMs' / (framework.name + '.dSYM')
    if not symbols.exists() and framework.name in args.allow_missing_framework_dsym:
        warnings.append(f'{framework.name}: matching dSYM absent (explicitly allowed); see release notes for provenance')
        framework_symbols.append({'framework': framework.name, 'uuid': framework_uuid.split()[1], 'status': 'missing dSYM explicitly recorded'})
        continue
    require(symbols.exists(), f'{framework.name} dSYM missing')
    identifier = framework_uuid.split()[1]
    require(identifier in run('dwarfdump', '--uuid', str(symbols)), f'{framework.name} dSYM UUID mismatch')
    framework_symbols.append({'framework': framework.name, 'uuid': identifier})
boot = app / 'Data/boot.config'
require(boot.exists(), 'Unity player data missing')
require('development-player=1' not in boot.read_text(), 'Development player must not be uploaded')
report = {
    'status': 'passed_with_warnings' if warnings else 'passed', 'warnings': warnings,
    'archive': str(args.archive), 'app': app.name,
    'bundleIdentifier': info['CFBundleIdentifier'], 'version': args.version, 'build': args.build,
    'team': profile['TeamIdentifier'][0], 'sdk': info.get('DTSDKName'),
    'minimumOS': info.get('MinimumOSVersion'), 'deviceFamilies': info['UIDeviceFamily'],
    'architectures': ['arm64'], 'codeSignature': 'verified', 'appDsymUUID': uuid,
    'frameworkDsyms': framework_symbols,
    'privacyManifests': privacy, 'executableSha256': hashlib.sha256(executable.read_bytes()).hexdigest(),
    'physicalDeviceTesting': 'not performed in this upload task'
}
args.report.parent.mkdir(parents=True, exist_ok=True)
args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print(json.dumps(report, ensure_ascii=False, indent=2))
