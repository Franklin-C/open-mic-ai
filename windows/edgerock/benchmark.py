"""Real checkpoint smoke checks and latency measurements, using synthetic inputs."""
import argparse
import base64
import io
import json
from pathlib import Path
import time

from server import EdgeRock, complete


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, default=Path(__file__).resolve().parents[1] / "models/edgerock-4b")
    parser.add_argument("--device", choices=["cpu", "cuda", "auto"], default="auto")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    started = time.perf_counter()
    try:
        engine = EdgeRock(args.model, args.device)
    except Exception as error:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps({"complete": False, "error": str(error), "load_seconds": time.perf_counter()-started}, indent=2))
        raise
    report = {"device": engine.device, "load_seconds": time.perf_counter() - started, "cases": []}
    if engine.device == "cuda":
        report["gpu"] = engine.torch.cuda.get_device_name()
    print(f"Loaded in {report['load_seconds']:.2f}s on {engine.device}", flush=True)
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 70)
    image = Image.new("RGB", (640, 240), "white")
    draw = ImageDraw.Draw(image)
    draw.text((40, 70), "OPEN MIC 42", font=font, fill="black")
    data = io.BytesIO()
    image.save(data, format="PNG")
    url = "data:image/png;base64," + base64.b64encode(data.getvalue()).decode()
    cases = [
        ("text-edit", {"messages": [{"role": "system", "content": "Correct punctuation only. Return just the corrected text, without explanation."}, {"role": "user", "content": "please send Maya the blue folder before Friday"}], "max_tokens": 64}, lambda text: "Maya" in text and "Friday" in text and "blue folder" in text.lower()),
        ("layout-decision", {"messages": [{"role": "system", "content": 'Choose a layout for the source: prose for ordinary messages; list for independent shopping items. Return JSON with layout.'}, {"role": "user", "content": "For the picnic I need apples, pears and two bananas."}], "max_tokens": 32, "response_format": {"json_schema": {"schema": {"type": "object", "properties": {"layout": {"enum": ["prose", "list"]}}, "required": ["layout"], "additionalProperties": False}}}}, lambda text: json.loads(text)["layout"] == "list"),
        ("image-reading", {"messages": [{"role": "system", "content": "Read the attached image. Give a concise answer with no explanation."}, {"role": "user", "content": [{"type": "text", "text": "What exact text and number are shown?"}, {"type": "image_url", "image_url": {"url": url}}]}], "max_tokens": 64}, lambda text: "42" in text and "OPEN" in text.upper() and "MIC" in text.upper()),
    ]
    try:
        for repeat in range(2):
            for name, request, check in cases:
                started = time.perf_counter()
                row = {"case": name, "repeat": repeat}
                try:
                    response = complete(engine, request)
                    row.update(seconds=time.perf_counter()-started, output=response["choices"][0]["message"]["content"], tokens=response["usage"]["completion_tokens"])
                    row["pass"] = check(row["output"])
                except Exception as error:
                    row.update(seconds=time.perf_counter()-started, error=str(error), **{"pass": False})
                report["cases"].append(row)
                print(json.dumps(row), flush=True)
        report["complete"] = all(row["pass"] for row in report["cases"])
        if engine.device == "cuda":
            report["peak_gpu_gib"] = engine.torch.cuda.max_memory_allocated() / 2**30
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return 0 if report.get("complete") else 1


if __name__ == "__main__":
    raise SystemExit(run())
