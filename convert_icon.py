from PIL import Image
import sys

try:
    img = Image.open('src/MonitorFusion.App/Assets/icon.png')
    # Resize to standard icon sizes
    icon_sizes = [(16,16), (32, 32), (48, 48), (64,64)]
    img.save('src/MonitorFusion.App/Assets/tray-icon.ico', format='ICO', sizes=icon_sizes)
    print('Converted successfully')
except Exception as e:
    print(f'Error: {e}')
