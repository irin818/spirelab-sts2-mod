import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


spec = importlib.util.spec_from_file_location(
    "verify_witch_release", Path(__file__).resolve().parents[1] / "scripts/verify-witch-release.py"
)
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)


class WorkshopReleaseTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.release = self.root / "release"
        self.content = self.root / "workshop/content"
        self.mods = self.root / "tested-mods"
        self.payload = {
            "SpireLab.dll": b"MZ\x01\x00\x14SpireLab.PackageKind\x0fWorkshopRelease\x00\x00",
            "SpireLab.pck": b"GDPC res://SpireLab/character.png",
            "SpireLab.json": b'{"version":"v1.6.2"}',
        }
        for directory in (self.release, self.content, self.mods / "SpireLab"):
            directory.mkdir(parents=True)
            for name, data in self.payload.items():
                (directory / name).write_bytes(data)
        self.manifest = self.root / "SpireLab/SpireLab.json"
        self.manifest.parent.mkdir()
        self.manifest.write_bytes(self.payload[self.manifest.name])

    def verify(self, mode, path):
        with patch.multiple(guard, ROOT=self.root, RELEASE=self.release):
            guard.verify(mode, path)

    def test_verified_release_is_accepted_for_staging_and_upload(self):
        self.verify("tested-mods", self.mods)
        self.verify("workshop", self.content.parent)

    def test_debug_assembly_at_release_path_is_rejected(self):
        (self.release / "SpireLab.dll").write_bytes(b"MZ Debug build")
        with self.assertRaisesRegex(ValueError, "Missing ExportRelease build stamp"):
            self.verify("pack", None)

    def test_test_lab_resource_inside_release_pck_is_rejected(self):
        (self.release / "SpireLab.pck").write_bytes(b"GDPC res://SpireLab/testlab/panel.tscn")
        with self.assertRaisesRegex(ValueError, "Test Lab code or resource paths"):
            self.verify("pack", None)

    def test_test_lab_session_is_rejected_even_with_release_mod_files(self):
        (self.mods / "SpireLab.TestLab").mkdir()
        with self.assertRaisesRegex(ValueError, "Test Lab session"):
            self.verify("tested-mods", self.mods)

    def test_modified_workshop_file_is_rejected(self):
        (self.content / "SpireLab.pck").write_bytes(b"GDPC different build")
        with self.assertRaisesRegex(ValueError, "differs from"):
            self.verify("workshop", self.content.parent)

    def test_release_built_before_manifest_change_is_rejected(self):
        self.manifest.write_bytes(b'{"version":"v1.7.0"}')
        with self.assertRaisesRegex(ValueError, "current Mod manifest has changed"):
            self.verify("workshop", self.content.parent)

    def test_workshop_content_with_extra_folder_is_rejected(self):
        (self.content / "extras").mkdir()
        with self.assertRaisesRegex(ValueError, "unexpected"):
            self.verify("workshop", self.content.parent)

    def test_workshop_content_with_symlink_is_rejected(self):
        path = self.content / "SpireLab.dll"
        path.unlink()
        path.symlink_to(self.release / path.name)
        with self.assertRaisesRegex(ValueError, "nonempty regular file"):
            self.verify("workshop", self.content.parent)
