import os
import json
import re
import matplotlib.pyplot as plt

def compare_fps(target_x):
    # Sanear target_x para evitar espacios o saltos de línea invisibles
    clean_target_x = str(target_x).strip()

    # Obtener las rutas relativas de los directorios
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))
    graphics_dir = os.path.abspath(os.path.join(script_dir, "..", "graphics"))

    if not os.path.exists(data_dir):
        print(f"Error: Directory '{data_dir}' not found.")
        return

    # Expresión regular: TelemetryData_{X} que NO esté seguido inmediatamente por un número (?!\d)
    pattern = re.compile(rf"^TelemetryData_{clean_target_x}(?!\d)_?(.*)\.json$")

    # Filtrar los archivos de la carpeta
    matched_files = []
    for filename in os.listdir(data_dir):
        match = pattern.match(filename)
        if match:
            matched_files.append((filename, match.group(1)))

    if not matched_files:
        print(f"No matching files found for experiment X = {clean_target_x} in '{data_dir}'.")
        return

    plt.figure(figsize=(12, 6))
    processed_count = 0
    fps_averages = {}

    print(f"\n==================================================")
    print(f"--- FPS COMPARISON FOR EXPERIMENT X = {clean_target_x} ---")
    print(f"==================================================")

    for json_filename, y_label in sorted(matched_files):
        file_path = os.path.join(data_dir, json_filename)

        if not y_label:
            y_label = "Default"

        # Cargar datos del archivo JSON
        with open(file_path, 'r', encoding='utf-8') as f:
            try:
                data = json.load(f)
            except Exception as e:
                print(f"Error reading '{json_filename}': {e}")
                continue

        if not data:
            print(f"Warning: File '{json_filename}' is empty.")
            continue

        # Extraer marcas de tiempo y FPS
        timestamps = [frame.get("timeStamp", 0.0) for frame in data]
        fps_list = [frame.get("fps", 0.0) for frame in data]

        if not fps_list:
            print(f"Warning: No FPS data found in '{json_filename}'.")
            continue

        # Cálculo de la media de FPS
        avg_fps = sum(fps_list) / len(fps_list)
        fps_averages[y_label] = avg_fps

        # Añadir la serie a la gráfica con la media en la etiqueta
        plt.plot(timestamps, fps_list, label=f"{y_label} (Avg: {avg_fps:.2f} FPS)", linewidth=2)
        processed_count += 1
        print(f"• Added series '{y_label}' ({len(fps_list)} samples) | Avg FPS: {avg_fps:.2f}")

    if processed_count == 0:
        print("No valid telemetry data could be processed.")
        plt.close()
        return

    # Resumen general en consola
    print("\n--- AVERAGE FPS SUMMARY ---")
    for label, avg in fps_averages.items():
        print(f"  • {label}: {avg:.2f} FPS")

    # Configuración de la gráfica
    plt.title(f"FPS Comparison Over Time (Experiment {clean_target_x} agents)", fontsize=14)
    plt.xlabel("Simulation Time (seconds)", fontsize=12)
    plt.ylabel("Frames Per Second (FPS)", fontsize=12)
    plt.grid(True, linestyle=":", alpha=0.6)
    plt.legend(title="Variant (Y)", loc="upper right")
    plt.tight_layout()

    # Creación y normalización segura de la ruta de salida para Windows
    os.makedirs(graphics_dir, exist_ok=True)
    output_image = os.path.normpath(os.path.join(graphics_dir, f"fps_comparison_experiment_{clean_target_x}.png"))

    plt.savefig(output_image, dpi=300)
    plt.close()

    print("\n" + "=" * 50)
    print(f"Comparison graph saved to: {output_image}")
    print("=" * 50 + "\n")

if __name__ == "__main__":
    TARGET_X = 5000
    compare_fps(TARGET_X)