
import os

def create_index_file(directory):
    index_file_path = os.path.join(directory, 'index.html')
    if os.path.exists(index_file_path):
        os.remove(index_file_path)

    with open(index_file_path, 'w') as f:
        current_dirctory = os.path.basename(directory)
        f.write(f"<html><body><h1>Index of {current_dirctory}</h1><ul>")
        for item in os.listdir(directory):
            item_path = os.path.join(directory, item)
            if os.path.isdir(item_path):
                if item == 'bin':
                    description = "Compiled files"
                elif item == 'obj':
                    description = "Object files"
                elif item == 'TestResults':
                    description = "Test results files"
                elif item == '.git':
                    description = "Git repository files"
                elif item == '.vs':
                    description = "Visual Studio files"
                elif item == 'doc':
                    description = "Documentation files"
                else:
                    description = "Directory"
                f.write(f"<li>{item} - {description}<ul>")

                if item.startswith('.') or item == 'bin' or item == 'obj' or item == 'TestResults':
                    f.write("</ul></li>")
                    continue

                list_sub_items(item_path, f)
                f.write("</ul></li>")
            else:
                f.write(f"<li>{item}</li>")
        f.write("</ul></body></html>")
    print(f"Created index.html in {directory}")

def list_sub_items(directory, file_handle):
    index_file_path = os.path.join(directory, 'index.html')
    if os.path.exists(index_file_path):
        os.remove(index_file_path)

    for item in os.listdir(directory):
        index_file_path = os.path.join(directory, 'index.html')
        if os.path.exists(index_file_path):
            os.remove(index_file_path)

        item_path = os.path.join(directory, item)
        if os.path.isdir(item_path):
            if item == 'bin':
                description = "Compiled files"
            elif item == 'obj':
                description = "Object files"
            elif item == 'TestResults':
                description = "Test results files"
            elif item == '.git':
                description = "Git repository files"
            elif item == '.vs':
                description = "Visual Studio files"
            elif item == 'doc':
                description = "Documentation files"
            else:
                description = "Directory"
            f.write(f"<li>{item} - {description}<ul>")

            if item.startswith('.') or item == 'bin' or item == 'obj' or item == 'TestResults':
                f.write("</ul></li>")
                continue

            list_sub_items(item_path, file_handle)
            file_handle.write("</ul></li>")
        else:
            file_handle.write(f"<li>{item}</li>")

def main():
    root_directory = os.getcwd()
    create_index_file(root_directory)

if __name__ == "__main__":
    main()
