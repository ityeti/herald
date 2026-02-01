"""Quick diagnostic for OCR issues."""
import sys
sys.path.insert(0, 'src')

print("=== Herald OCR Diagnostic ===\n")

# Test 1: Check clipboard for image
print("1. Checking clipboard for image...")
try:
    from PIL import ImageGrab, Image
    clip = ImageGrab.grabclipboard()
    print(f"   Clipboard type: {type(clip)}")
    if clip is None:
        print("   Result: No image in clipboard")
    elif isinstance(clip, Image.Image):
        print(f"   Result: Found image! Size: {clip.size[0]}x{clip.size[1]}")
        clip.save("clipboard_test.png")
        print("   Saved to clipboard_test.png for inspection")
    elif isinstance(clip, list):
        print(f"   Result: File list: {clip}")
    else:
        print(f"   Result: Unknown type")
except Exception as e:
    print(f"   Error: {e}")

# Test 2: Check winocr
print("\n2. Testing winocr...")
try:
    import winocr
    print("   winocr imported successfully")
except ImportError as e:
    print(f"   Error importing winocr: {e}")

# Test 3: Try OCR on clipboard image
print("\n3. Testing OCR on clipboard image...")
try:
    from PIL import ImageGrab, Image
    import asyncio
    import winocr

    clip = ImageGrab.grabclipboard()
    if isinstance(clip, Image.Image):
        width, height = clip.size
        print(f"   Processing {width}x{height} image...")

        # Convert to RGBA raw bytes (what winocr expects)
        if clip.mode != 'RGBA':
            clip = clip.convert('RGBA')
        image_bytes = clip.tobytes()
        print(f"   Image size: {len(image_bytes)} bytes (RGBA raw)")

        # Run OCR with width and height
        async def run_ocr():
            result = await winocr.recognize_bytes(image_bytes, width, height, lang='en')
            return result

        result = asyncio.run(run_ocr())
        print(f"   OCR result type: {type(result)}")
        if result:
            text = result.text if hasattr(result, 'text') else str(result)
            print(f"   Text length: {len(text)} chars")
            print(f"   First 200 chars: {text[:200]}")
        else:
            print("   OCR returned None")
    else:
        print("   No image in clipboard to test OCR")
except Exception as e:
    import traceback
    print(f"   Error: {e}")
    traceback.print_exc()

# Test 4: Test region capture subprocess
print("\n4. Testing region capture subprocess...")
try:
    import subprocess
    import tempfile
    from pathlib import Path

    # Simple test - just check if subprocess works
    result = subprocess.run(
        [sys.executable, "-c", "print('subprocess works')"],
        capture_output=True,
        text=True,
        timeout=5
    )
    print(f"   Subprocess stdout: {result.stdout.strip()}")
    print(f"   Subprocess stderr: {result.stderr.strip() if result.stderr else '(none)'}")
    print(f"   Return code: {result.returncode}")
except Exception as e:
    print(f"   Error: {e}")

print("\n=== Diagnostic Complete ===")
print("\nTo test: Take a screenshot with Win+Shift+S, then run this script.")
input("Press Enter to exit...")
