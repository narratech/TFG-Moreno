import os
import json
import glob
import math
import re
import pandas as pd

# ==============================================================================
# CONFIGURACIÓN DE PADDING POR TIPO DE MUNDO / ESPACIO
# Ajusta aquí la tolerancia de distancia (unidades) según la escala del mapa.
# ==============================================================================
PADDING_BY_WORLD = {
    "Voxel": 2.0,
    "Grid": 2.0,
    "Geodesic": 2.0
}
DEFAULT_PADDING = 1.5
DEFAULT_OFFSET_THRESHOLD = 0.95


def load_json_data(file_path):
    if not os.path.exists(file_path):
        return None
    with open(file_path, 'r', encoding='utf-8') as f:
        try:
            return json.load(f)
        except Exception:
            return None


def vector_distance(v1, v2):
    return math.sqrt((v1['x'] - v2['x'])**2 + (v1['y'] - v2['y'])**2 + (v1['z'] - v2['z'])**2)


def evaluate_single_file(file_path, position_padding, offset_threshold):
    data = load_json_data(file_path)
    if not data or 'agents' not in data:
        return None

    agents = data['agents']
    total_agents = len(agents)

    if total_agents == 0:
        return None

    arrived_count = 0
    well_positioned_count = 0
    both_criteria_count = 0

    for agent in agents:
        sample_pos = agent['samplePosition']
        target_pos = agent['targetNodePosition']
        offset_pct = agent['offsetPercentage']

        dist = vector_distance(sample_pos, target_pos)

        has_arrived = dist <= position_padding
        is_well_positioned = offset_pct >= offset_threshold

        if has_arrived:
            arrived_count += 1
        if is_well_positioned:
            well_positioned_count += 1
        if has_arrived and is_well_positioned:
            both_criteria_count += 1

    return {
        "Arrival_Rate": (arrived_count / total_agents) * 100,
        "Slot_Rate": (well_positioned_count / total_agents) * 100,
        "Overall_Success": (both_criteria_count / total_agents) * 100,
        "Total_Agents_Json": total_agents
    }


def evaluate_all_formations(offset_threshold=DEFAULT_OFFSET_THRESHOLD):
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))

    if not os.path.exists(data_dir):
        print(f"Error: Data directory not found at '{data_dir}'")
        return

    # Buscar archivos con el formato: FormationData_{N}Agents_{Mundo}.json
    search_pattern = os.path.join(data_dir, "FormationData_*.json")
    files = glob.glob(search_pattern)

    if not files:
        print(f"No files matching 'FormationData_*.json' found in '{data_dir}'.")
        return

    # Regex para extraer (Número de Agentes) y (Tipo de Mundo)
    file_regex = re.compile(r"^FormationData_(\d+)Agents_([A-Za-z0-9]+)\.json$")

    summary_records = []

    for file_path in files:
        filename = os.path.basename(file_path)
        match = file_regex.match(filename)

        if not match:
            continue

        num_agents = int(match.group(1))
        world_type = match.group(2)

        # Seleccionar padding según el espacio/mundo o usar valor por defecto
        padding = PADDING_BY_WORLD.get(world_type, DEFAULT_PADDING)

        metrics = evaluate_single_file(file_path, padding, offset_threshold)
        if not metrics:
            continue

        summary_records.append({
            "World Type": world_type,
            "Agents": num_agents,
            "Padding (u)": padding,
            "Arrival Rate (%)": round(metrics["Arrival_Rate"], 2),
            "Slot Rate (%)": round(metrics["Slot_Rate"], 2),
            "Overall Success (%)": round(metrics["Overall_Success"], 2)
        })

    if not summary_records:
        print("No valid formation files matched the naming pattern.")
        return

    # Crear DataFrame y ordenar por Tipo de Mundo y Cantidad de Agentes
    df = pd.DataFrame(summary_records)
    df = df.sort_values(by=["World Type", "Agents"]).reset_index(drop=True)

    # Crear la fila total con la suma de agentes y el promedio de los porcentajes
    total_row = pd.DataFrame([{
        "World Type": "TOTAL / AVERAGE",
        "Agents": df["Agents"].sum(),
        "Padding (u)": "-",
        "Arrival Rate (%)": round(df["Arrival Rate (%)"].mean(), 2),
        "Slot Rate (%)": round(df["Slot Rate (%)"].mean(), 2),
        "Overall Success (%)": round(df["Overall Success (%)"].mean(), 2)
    }])

    # Concatenar la fila resumen al final de la tabla
    df_final = pd.concat([df, total_row], ignore_index=True)

    # Imprimir tabla en consola
    print("\n==========================================================================================")
    print(f"=== FORMATION ACCURACY SUMMARY TABLE (Threshold: {offset_threshold * 100:.0f}%) ===")
    print("==========================================================================================\n")
    print(df_final.to_string(index=False))
    print("\n" + "=" * 90 + "\n")


if __name__ == "__main__":
    evaluate_all_formations()