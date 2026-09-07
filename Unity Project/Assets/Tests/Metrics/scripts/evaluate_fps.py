import json
import statistics
import os
import glob
import matplotlib.pyplot as plt

def process_single_fps_telemetry(data_dir, graphics_dir, json_filename):
    json_path = os.path.join(data_dir, json_filename)

    # Extraer el sufijo X eliminando 'TelemetryData' y '.json'
    # Ejemplo: TelemetryData1.json -> '1', TelemetryData.json -> ''
    suffix = json_filename[len("TelemetryData"):-5]

    # 1. Leer archivo JSON
    with open(json_path, 'r', encoding='utf-8') as file:
        data = json.load(file)

    if not data:
        print(f"Telemetry file '{json_filename}' is empty.")
        return

    # Extraer datos
    timestamps = [frame.get("timeStamp", 0.0) for frame in data]
    fps_list = [frame.get("fps", 0.0) for frame in data]

    if not fps_list:
        print(f"No FPS data found in '{json_filename}'.")
        return

    # 2. Imprimir tabla en consola
    print(f"\n--- Processing: {json_filename} ---")
    print(f"{'SAMPLE':<8} | {'TIME (s)':<12} | {'FPS':<10}")
    print("-" * 36)
    
    for i, (t, fps) in enumerate(zip(timestamps, fps_list), start=1):
        print(f"{i:<8} | {t:<12.2f} | {fps:<10.2f}")

    # 3. Calcular métricas
    total_frames = len(fps_list)
    avg_fps = statistics.mean(fps_list)
    min_fps = min(fps_list)
    max_fps = max(fps_list)
    median_fps = statistics.median(fps_list)

    # 4. Imprimir resumen
    print("\n" + "=" * 36)
    print(f"    PERFORMANCE METRICS ({json_filename})")
    print("=" * 36)
    print(f"Total Samples    : {total_frames}")
    print(f"Average FPS      : {avg_fps:.2f}")
    print(f"Median FPS       : {median_fps:.2f}")
    print(f"Minimum FPS      : {min_fps:.2f}")
    print(f"Maximum FPS      : {max_fps:.2f}")
    print("=" * 36)

    # 5. Generar gráfica
    plt.figure(figsize=(10, 5))
    plt.plot(timestamps, fps_list, label="FPS", color="#1f77b4", linewidth=2)
    plt.axhline(y=avg_fps, color="r", linestyle="--", label=f"Average FPS ({avg_fps:.1f})")

    plt.title(f"FPS Performance Over Time ({json_filename})")
    plt.xlabel("Time (seconds)")
    plt.ylabel("Frames Per Second (FPS)")
    plt.grid(True, linestyle=":", alpha=0.6)
    plt.legend()
    plt.tight_layout()

    # Guardar gráfica en ../graphics/fps_performanceX.png
    plot_filename = f"fps_performance{suffix}.png"
    plot_path = os.path.join(graphics_dir, plot_filename)
    plt.savefig(plot_path, dpi=300)
    plt.close()

    print(f"Plot successfully saved to: {plot_path}")

def process_fps_telemetry():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))
    graphics_dir = os.path.abspath(os.path.join(script_dir, "..", "graphics"))

    if not os.path.exists(data_dir):
        print(f"Error: Directory '{data_dir}' not found.")
        return

    # Buscar todos los archivos que coincidan con TelemetryData*.json
    search_pattern = os.path.join(data_dir, "TelemetryData*.json")
    files = glob.glob(search_pattern)

    if not files:
        print(f"No files matching 'TelemetryData*.json' found in '{data_dir}'.")
        return

    os.makedirs(graphics_dir, exist_ok=True)

    # Procesar cada archivo encontrado en orden
    for file_path in sorted(files):
        json_filename = os.path.basename(file_path)
        process_single_fps_telemetry(data_dir, graphics_dir, json_filename)

if __name__ == "__main__":
    process_fps_telemetry()