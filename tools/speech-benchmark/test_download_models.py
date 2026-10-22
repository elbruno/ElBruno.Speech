"""Model-free checks for the explicit benchmark downloader."""

import hashlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import download_models


class DownloadTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.data = b"test model"
        self.asset = ("test", "owner/model", "a" * 40, "model.bin", len(self.data),
                      hashlib.sha256(self.data).hexdigest())
        self.path = self.root / "test" / "model.bin"
        self.assets = patch.object(download_models, "ASSETS", [self.asset])
        self.assets.start()
        self.addCleanup(self.assets.stop)

    def test_download_verifies_and_then_reuses_without_network(self):
        with patch("urllib.request.urlopen", return_value=io.BytesIO(self.data)) as request:
            download_models.download(self.root)
            self.assertEqual(self.data, self.path.read_bytes())
            self.assertIn("/resolve/" + "a" * 40 + "/model.bin", request.call_args.args[0])
        with patch("urllib.request.urlopen", side_effect=AssertionError("unexpected network")):
            download_models.download(self.root)

    def test_rejects_same_length_corruption_and_removes_partial_download(self):
        with patch("urllib.request.urlopen", return_value=io.BytesIO(b"x" * len(self.data))):
            with self.assertRaisesRegex(ValueError, "SHA-256"):
                download_models.download(self.root)
        self.assertFalse(self.path.exists())
        self.assertFalse(self.path.with_suffix(".bin.download").exists())

    def test_rejects_oversized_download(self):
        with patch("urllib.request.urlopen", return_value=io.BytesIO(self.data + b"x")):
            with self.assertRaisesRegex(ValueError, "exceeds"):
                download_models.download(self.root)
        self.assertFalse(self.path.exists())
        self.assertFalse(self.path.with_suffix(".bin.download").exists())

    def test_existing_corruption_is_not_overwritten(self):
        self.path.parent.mkdir()
        self.path.write_bytes(b"x" * len(self.data))
        with patch("urllib.request.urlopen", side_effect=AssertionError("unexpected network")):
            with self.assertRaises(ValueError):
                download_models.download(self.root)
        self.assertEqual(b"x" * len(self.data), self.path.read_bytes())

    def test_interrupted_download_requires_explicit_cleanup(self):
        self.path.parent.mkdir()
        partial = self.path.with_suffix(".bin.download")
        partial.write_bytes(b"interrupted")
        with self.assertRaisesRegex(ValueError, "interrupted"):
            download_models.download(self.root)
        self.assertEqual(b"interrupted", partial.read_bytes())


if __name__ == "__main__":
    unittest.main()
