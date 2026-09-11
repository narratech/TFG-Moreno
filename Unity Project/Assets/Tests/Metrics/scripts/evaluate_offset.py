import os
import json
import pandas as pd
import matplotlib.pyplot as plt

def process_multiple_offset_telemetry(file_list, output_name="combined_offset_telemetry.png"):
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))
    graphics_dir = os.path.abspath(os.path.join(script_dir, "..", "graphics"))

    if not os.path.exists(data_dir):
        print(f"Error: Data directory not found at '{data_dir}'")
        return

    plt.figure(figsize=(12, 6))
    processed_count = 0

    print(f"\n==================================================")
    print(f"--- COMBINING OFFSET TELEMETRY FOR SELECTED FILES ---")
    print(f"==================================================")

    for filename in file_list:
        json_path = os.path.normpath(os.path.join(data_dir, filename))

        if not os.path.exists(json_path):
            print(f"Warning: File '{filename}' not found in '{data_dir}'. Skipping.")
            continue

        with open(json_path, "r", encoding="utf-8") as f:
            try:
                frames = json.load(f)
            except Exception as e:
                print(f"Error reading '{filename}': {e}")
                continue

        if not frames:
            print(f"Warning: File '{filename}' is empty. Skipping.")
            continue

        records = []
        for frame in frames:
            time_stamp = frame.get("timeStamp", 0.0)
            std_agents = frame.get("standardAgents", [])
            ecs_agents = frame.get("ecsAgents", [])

            std_offsets = [a["offsetPercentage"] * 100 for a in std_agents if "offsetPercentage" in a]
            ecs_offsets = [a["offsetPercentage"] * 100 for a in ecs_agents if "offsetPercentage" in a]
            total_offsets = std_offsets + ecs_offsets

            avg_total = sum(total_offsets) / len(total_offsets) if total_offsets else 0.0

            records.append({
                "TimeStamp": time_stamp,
                "Offset_Total_Avg": avg_total
            })

        df = pd.DataFrame(records)

        if df.empty:
            continue

        # Generar una etiqueta limpia para la leyenda usando el nombre del archivo
        label_name = os.path.splitext(filename)[0].replace("TelemetryData", "").strip("_")
        if not label_name:
            label_name = filename

        plt.plot(df["TimeStamp"], df["Offset_Total_Avg"], label=label_name, linewidth=2)
        processed_count += 1
        print(f"• Added series '{label_name}' from '{filename}' ({len(df)} frames).")

    if processed_count == 0:
        print("No valid telemetry data could be processed.")
        plt.close()
        return

    # Configuración de la gráfica combinada
    plt.title("Mean Offset Percentage Comparison Over Time", fontsize=14)
    plt.xlabel("Simulation Time (seconds)", fontsize=12)
    plt.ylabel("Mean Offset (%)", fontsize=12)
    plt.ylim(0, 105)
    plt.grid(True, linestyle=":", alpha=0.6)
    plt.legend(title="Files / Variants", loc="upper right")
    plt.tight_layout()

    # Guardar gráfica combinada con ruta limpia
    os.makedirs(graphics_dir, exist_ok=True)
    output_image = os.path.normpath(os.path.join(graphics_dir, output_name))

    plt.savefig(output_image, dpi=300)
    plt.close()

    print("\n" + "=" * 50)
    print(f"Combined plot saved to: {output_image}")
    print("=" * 50 + "\n")


if __name__ == "__main__":
    # Define la lista con los nombres exactos de los archivos que quieras comparar
    TARGET_FILES = [
        "TelemetryData_offset_no_obstacles.json",
        "TelemetryData_offset_obstacles.json"
    ]
    
    # Opcional: puedes personalizar el nombre de la imagen final
    OUTPUT_IMAGE_NAME = "offset_comparison_selected.png"

    process_multiple_offset_telemetry(TARGET_FILES, OUTPUT_IMAGE_NAME)