#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export MACOSX_DEPLOYMENT_TARGET=13.0
swift test
swift build -c release
runtime="$PWD/.build/runtime"
mkdir -p "$runtime"
build_engine() {
  local name="$1" repository="$2" revision="$3" target="$4"
  local checkout="$PWD/.build/$name-source"
  if [[ ! -d "$checkout/.git" ]]; then git init "$checkout"; git -C "$checkout" remote add origin "$repository"; fi
  git -C "$checkout" fetch --depth=1 origin "$revision"
  git -C "$checkout" checkout --detach FETCH_HEAD
  cmake -S "$checkout" -B "$checkout/build" -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_DEPLOYMENT_TARGET=13.0 -DBUILD_SHARED_LIBS=OFF -DGGML_OPENMP=OFF -DGGML_METAL=ON -DGGML_METAL_EMBED_LIBRARY=ON -DGGML_METAL_SHADER_DEBUG=OFF -DLLAMA_CURL=OFF
  cmake --build "$checkout/build" --config Release --parallel 3 --target "$target"
  cp "$checkout/build/bin/$target" "$runtime/$target"
  cp "$checkout/LICENSE" "$runtime/$name-LICENSE.txt"
}
build_engine whisper https://github.com/ggml-org/whisper.cpp.git d2a764c648779feb36d44363f212a29b00aad1af whisper-server
build_engine llama https://github.com/ggml-org/llama.cpp.git 69ecae922d2b60c1fa8c590279f4e2099af75111 llama-server
binary="$(swift build -c release --show-bin-path)"
app="$PWD/dist/Open Mic AI.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources/runtime"
cp "$binary/OpenMicAI" "$app/Contents/MacOS/"
cp -R "$binary/OpenMicAI_OpenMicAI.bundle" "$app/Contents/Resources/"
cp Sources/OpenMicAI/Resources/models.json "$app/Contents/Resources/models.json"
cp "$runtime/"* "$app/Contents/Resources/runtime/"
cp Info.plist "$app/Contents/Info.plist"
if [[ -f Sources/OpenMicAI/Resources/logo.png ]]; then
  iconset="$PWD/.build/OpenMicAI.iconset"; mkdir -p "$iconset"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" Sources/OpenMicAI/Resources/logo.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2)); sips -z "$double" "$double" Sources/OpenMicAI/Resources/logo.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$iconset" -o "$app/Contents/Resources/OpenMicAI.icns"
fi
codesign --force --deep --sign - "$app"
"$app/Contents/MacOS/OpenMicAI" --self-test
ditto -c -k --sequesterRsrc --keepParent "$app" "$PWD/dist/OpenMicAI-macOS-$(uname -m).zip"
echo "Built $app (development signature; not notarized)"
