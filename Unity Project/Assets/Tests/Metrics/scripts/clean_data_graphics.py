import os
import shutil

def clear_directory(dir_path):
    """
    Elimina todos los archivos y subcarpetas dentro del directorio especificado,
    sin borrar la carpeta contenedora.
    """
    if not os.path.exists(dir_path):
        print(f"Directory not found: '{dir_path}' (Skipped)")
        return 0

    deleted_count = 0
    for item in os.listdir(dir_path):
        item_path = os.path.join(dir_path, item)
        try:
            if os.path.isfile(item_path) or os.path.islink(item_path):
                os.remove(item_path)
                deleted_count += 1
            elif os.path.isdir(item_path):
                shutil.rmtree(item_path)
                deleted_count += 1
        except Exception as e:
            print(f"Error deleting '{item_path}': {e}")

    return deleted_count

def clean_data_and_graphics():
    # Obtener la ruta absoluta del directorio del script
    script_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Definir las rutas relativas de las carpetas 'data' y 'graphics' (subiendo un nivel desde la carpeta de scripts)
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))
    graphics_dir = os.path.abspath(os.path.join(script_dir, "..", "graphics"))

    print("\n==================================================")
    print("--- CLEANING DATA AND GRAPHICS DIRECTORIES ---")
    print("==================================================")

    # Limpiar carpeta 'data'
    data_deleted = clear_directory(data_dir)
    print(f"• Data Directory ('{data_dir}'): {data_deleted} items removed.")

    # Limpiar carpeta 'graphics'
    graphics_deleted = clear_directory(graphics_dir)
    print(f"• Graphics Directory ('{graphics_dir}'): {graphics_deleted} items removed.")

    print("\nCleanup completed successfully.")
    print("==================================================\n")

if __name__ == "__main__":
    clean_data_and_graphics()