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

    def _write_png_stub(self, path, width, height, pad=0):
        import struct
        ihdr = struct.pack(">II", width, height) + b"\x08\x02\x00\x00\x00"
        path.write_bytes(b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR" + ihdr + (b"\x00" * pad))

    def _write_gif_stub(self, path, width, height, frames=2, pad=0):
        import struct
        gce = b"\x21\xf9\x04\x00\x0a\x00\x00\x00" * frames
        path.write_bytes(b"GIF89a" + struct.pack("<HH", width, height) + gce + (b"\x00" * pad))

    def test_workshop_rejects_mp4_disguised_as_image_png(self):
        (self.content.parent / "image.png").write_bytes(b"\x00\x00\x00\x18ftypmp42")
        with self.assertRaisesRegex(ValueError, "valid PNG"):
            self.verify("workshop", self.content.parent)

    def test_media_validation_accepts_complete_deliverables(self):
        media = self.root / "media"
        media.mkdir()
        self._write_png_stub(media / "image.png", 800, 800)
        self._write_png_stub(media / "cover-master-1600.png", 1600, 1600)
        self._write_png_stub(media / "video-cover.png", 1920, 1080)
        self._write_gif_stub(media / "cover.gif", 540, 540, frames=24)
        (media / "trailer.mp4").write_bytes(b"\x00\x00\x00\x18ftypmp42")
        probe = (
            '{"format":{"duration":"10.000000"},"streams":['
            '{"codec_type":"video","codec_name":"h264","width":1920,"height":1080},'
            '{"codec_type":"audio","codec_name":"aac"}]}'
        )
        with patch.object(guard, "_resolve_ffprobe", return_value="ffprobe"), \
             patch.object(guard.subprocess, "run") as run_mock:
            run_mock.return_value.stdout = probe
            guard.verify("media", media)

    def test_media_validation_rejects_oversized_cover_and_non_square_gif(self):
        media = self.root / "media-bad"
        media.mkdir()
        self._write_png_stub(media / "image.png", 800, 800, pad=1_000_000)
        with self.assertRaisesRegex(ValueError, "exceeds 1000000 bytes"):
            guard.verify("media", media)
        self._write_png_stub(media / "image.png", 800, 800)
        self._write_png_stub(media / "cover-master-1600.png", 1600, 1600)
        self._write_png_stub(media / "video-cover.png", 1920, 1080)
        self._write_gif_stub(media / "cover.gif", 640, 360, frames=10)
        (media / "trailer.mp4").write_bytes(b"\x00\x00\x00\x18ftypmp42")
        probe = (
            '{"format":{"duration":"10.000000"},"streams":['
            '{"codec_type":"video","codec_name":"h264","width":1920,"height":1080},'
            '{"codec_type":"audio","codec_name":"aac"}]}'
        )
        with patch.object(guard, "_resolve_ffprobe", return_value="ffprobe"), \
             patch.object(guard.subprocess, "run") as run_mock:
            run_mock.return_value.stdout = probe
            with self.assertRaisesRegex(ValueError, "1:1 square"):
                guard.verify("media", media)
