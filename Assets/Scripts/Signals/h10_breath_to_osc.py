import asyncio
import sys
import time
from collections import deque

from pythonosc.udp_client import SimpleUDPClient
from bleak import BleakScanner
from polar_python import PolarDevice, MeasurementSettings, SettingType

OSC_IP = "127.0.0.1"
OSC_PORT = 9011
OSC_WAVE01 = "/h10/breath_wave01"
OSC_AMP01  = "/h10/breath_amp01"
OSC_HR = "/h10/hr"

# save data
LOG_FILE = "h10_breath_log.txt"
log_file = open(LOG_FILE, "w")

# header
log_file.write("timestamp,raw_axis,breath_wave01,breath_amp01,env_span\n")
log_file.flush()

FS = 25.0
AXIS = "x"

DEMEAN_S = 5.0
SMOOTH_S = 0.35
WAVE_WIN_S = 8.0
AMP_WIN_S  = 10.0
CALIBRATE_S = 3.0

AMP_SCALE = 50.0

LOG_HZ = 1.0
PRINT_PACKET_SPAN = True

_seen_acc = False
_last_packet_t = 0.0
_packets = 0
_printed_packet = False

_last_wave01 = 0.5
_last_amp01 = 0.0
_last_env_span = 0.0
_last_packet_span = 0.0

osc = SimpleUDPClient(OSC_IP, OSC_PORT)

def axis_value(sample, axis: str) -> float:
    if isinstance(sample, (list, tuple)) and len(sample) >= 3:
        x, y, z = sample[0], sample[1], sample[2]
        return float(x if axis == "x" else y if axis == "y" else z)
    if isinstance(sample, dict):
        return float(sample.get(axis, 0.0))
    if hasattr(sample, axis):
        return float(getattr(sample, axis))
    return 0.0

class BreathWave:
    def __init__(self, fs: float):
        self.fs = fs
        self.start_t = None
        self.demean_buf = deque(maxlen=max(3, int(DEMEAN_S * fs)))
        self.smooth_buf = deque(maxlen=max(3, int(SMOOTH_S * fs)))
        self.wave_norm_buf = deque(maxlen=max(10, int(WAVE_WIN_S * fs)))
        self.amp_buf       = deque(maxlen=max(10, int(AMP_WIN_S * fs)))

    def update(self, raw: float, t: float):
        if self.start_t is None:
            self.start_t = t

        self.demean_buf.append(raw)
        mean = sum(self.demean_buf) / len(self.demean_buf)
        x = raw - mean

        self.smooth_buf.append(x)
        y = sum(self.smooth_buf) / len(self.smooth_buf)

        self.wave_norm_buf.append(y)
        self.amp_buf.append(y)

        ymin = min(self.wave_norm_buf)
        ymax = max(self.wave_norm_buf)
        denom = (ymax - ymin)
        if denom < 1e-6:
            wave01 = 0.5
        else:
            wave01 = (y - ymin) / denom
            wave01 = 0.0 if wave01 < 0.0 else 1.0 if wave01 > 1.0 else wave01

        span = (max(self.amp_buf) - min(self.amp_buf)) if len(self.amp_buf) >= 2 else 0.0
        amp01 = span / float(AMP_SCALE)
        amp01 = 0.0 if amp01 < 0.0 else 1.0 if amp01 > 1.0 else amp01

        if (t - self.start_t) < CALIBRATE_S:
            amp01 = 0.0

        return wave01, amp01, y, span

breath = BreathWave(FS)

def data_callback(data):
    global _seen_acc, _last_packet_t, _packets, _printed_packet
    global _last_wave01, _last_amp01, _last_env_span, _last_packet_span

    now = time.time()

    if isinstance(data, dict) and "data" in data:
        samples = data["data"]
    else:
        samples = getattr(data, "samples", [])

    if not samples:
        return

    if not _printed_packet:
        print("\n===== FIRST DATA PACKET =====")
        print("type(data):", type(data))
        if isinstance(data, dict):
            print("keys:", data.keys())
            print("first sample:", samples[0])
        print("=============================\n")
        _printed_packet = True

    if not _seen_acc:
        print(f"⭐ DATA FLOWING! Received {len(samples)} samples in first packet.")
        _seen_acc = True

    _packets += 1
    _last_packet_t = now

    dt = 1.0 / FS
    t = now - dt * (len(samples) - 1)

    if PRINT_PACKET_SPAN:
        vals = [axis_value(s, AXIS) for s in samples]
        _last_packet_span = (max(vals) - min(vals)) if vals else 0.0

    last_wave = 0.5
    last_amp  = 0.0
    last_span = 0.0

    
    for s in samples:
        v = axis_value(s, AXIS)
        wave01, amp01, _, span = breath.update(v, t)

        log_file.write(f"{t:.4f},{v:.3f},{wave01:.4f},{amp01:.4f},{span:.3f}\n")
        
        osc.send_message(OSC_WAVE01, float(wave01))
        osc.send_message(OSC_AMP01,  float(amp01))

        last_wave, last_amp, last_span = wave01, amp01, span
        t += dt

    _last_wave01 = float(last_wave)
    _last_amp01  = float(last_amp)
    _last_env_span = float(last_span)

async def main():
    print("Scanning for Polar H10…")
    device = await BleakScanner.find_device_by_filter(
        lambda bd, ad: bd.name and "Polar H10" in bd.name,
        timeout=10,
    )
    if not device:
        print("❌ Could not find Polar H10.")
        return

    acc_settings = MeasurementSettings(
        measurement_type="ACC",
        settings=[
            SettingType(type="SAMPLE_RATE", values=[int(FS)]),
            SettingType(type="RESOLUTION", values=[16]),
            SettingType(type="RANGE", values=[2]),
        ],
    )

    ppg_settings = MeasurementSettings(
        measurement_type="PPG",
        settings=[
            SettingType(type="SAMPLE_RATE", values=[100])
        ]
    )

    print(f"Connecting to {device.name}…")
    async with PolarDevice(device, data_callback) as polar:
        print(f"Starting ACC stream ({int(FS)}Hz)...")
        await polar.start_stream(acc_settings)
        await polar.start_stream(ppg_settings)

        for _ in range(10):
            if _seen_acc:
                break
            print("...waiting for first packet...")
            await asyncio.sleep(1.0)

        if not _seen_acc:
            print("❌ Connection timed out.")
            return

        print(
            f"✅ Sending OSC -> {OSC_IP}:{OSC_PORT}\n"
            f"  {OSC_WAVE01}  (0..1 breathing waveform)\n"
            f"  {OSC_AMP01}   (0..1 breathing strength)\n"
            f"Axis={AXIS}  (try x then z/y)\n"
            f"AMP_SCALE={AMP_SCALE}  (try 30/50/80)\n"
        )

        last_log = 0.0
        while True:
            await asyncio.sleep(0.1)

            if time.time() - _last_packet_t > 3.0:
                print("[WARN] Stale data!")

            if time.time() - last_log > (1.0 / max(0.1, LOG_HZ)):
                last_log = time.time()
                log_file.flush() #save file
                print(
                    f"[OK] packets={_packets} "
                    f"wave01={_last_wave01:.3f} "
                    f"amp01={_last_amp01:.4f} "
                    f"env_span={_last_env_span:.2f} "
                    f"packet_span={_last_packet_span:.2f}"
                )
        

if __name__ == "__main__":
    if sys.platform.startswith("win"):
        asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
    asyncio.run(main())
