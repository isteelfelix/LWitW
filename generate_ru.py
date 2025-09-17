import json
import os
from collections import defaultdict

def extract_strings(obj, strings):
    if isinstance(obj, str):
        strings.add(obj)
    elif isinstance(obj, dict):
        for value in obj.values():
            extract_strings(value, strings)
    elif isinstance(obj, list):
        for item in obj:
            extract_strings(item, strings)

def main():
    res_dir = 'res'
    if not os.path.exists(res_dir):
        print("res folder not found")
        return

    all_strings = set()

    for filename in os.listdir(res_dir):
        if filename.endswith('.json'):
            filepath = os.path.join(res_dir, filename)
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    extract_strings(data, all_strings)
            except Exception as e:
                print(f"Error parsing {filename}: {e}")

    # Filter strings: remove empty, very short, or non-text
    filtered_strings = {s for s in all_strings if s and len(s) > 1 and not s.isdigit()}

    # Create translations dict with empty values
    translations = {text: "" for text in sorted(filtered_strings)}

    # Write to ru.json
    with open('ru.json', 'w', encoding='utf-8') as f:
        json.dump(translations, f, ensure_ascii=False, indent=4)

    print(f"Generated ru.json with {len(translations)} entries")

if __name__ == "__main__":
    main()
