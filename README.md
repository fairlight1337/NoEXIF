# NoEXIF

## Description
NoEXIF is a utility that enhances your image file management by removing EXIF tags from images and renaming the images based on their SHA256 hash value. It integrates seamlessly into the Windows Explorer context menu, providing a straightforward and efficient way to manage image metadata and filenames directly from the file explorer.

## Features
- **Remove EXIF Data:** Strips EXIF metadata from images, ensuring that details such as camera settings, GPS location, and other sensitive information are removed. This feature maintains the original image orientation, ensuring that the visual appearance remains unchanged after EXIF removal.
- **Rename to Hash Value:** Renames image files to their SHA256 hash value, providing a unique and consistent naming scheme that helps in organizing and identifying images based on their content.

## Installation
1. Copy the built files into a folder of your choice.
2. Run the `NoEXIF.exe` executable once to register the context menu hooks in Windows Explorer.
3. After the initial setup, you can use the functionality transparently from the context menu.

Note: The application automatically updates the registry keys when moved and executed again. It conditionally requests elevated privileges to modify the registry keys when necessary.

## How it Works
NoEXIF integrates with the Windows Explorer context menu, offering two main functionalities:
1. Removing EXIF data from selected images while maintaining their original orientation.
2. Renaming images to their SHA256 hash value.

These options are accessible by right-clicking on image files or directories in Windows Explorer.

## Usage
Right-click on an image file or a directory in Windows Explorer:
- Choose "Remove EXIF Data" to strip the EXIF metadata from the image(s).
- Select "Rename to Hash Value" to rename the image(s) based on their SHA256 hash.

## Third-Party Libraries
- **Magick.NET:** Used for handling image metadata and processing.