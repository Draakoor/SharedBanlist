# SharedBanlist Plugin for IW4MAdmin

A plugin to share bans with other communitys & clans.

---

## 🔑 API Key Access

To use the shared banlist API, you will need an API key.  
Please contact **draakoor** via **Discord** to request one.  
Frontend: https://hsngaming.de/api/bans.php
<img width="1907" alt="image" src="https://github.com/user-attachments/assets/008acc38-9daf-4a44-bc89-a15e14680b20" />

---

## 📦 Installation

1. **Download the Plugin**  
   Go to the [Releases](https://github.com/draakoor/sharedbanlist/releases) section and download the latest `.dll` file.

2. **Install the Plugin**  
   Place the `.dll` in the `Plugins` folder of your IW4MAdmin installation.

3. **Restart IW4MAdmin**

4. **Configure the Plugin**  
   Create or edit the following file:  
   `Configuration/sharedbanlist.json`

   Example configuration:
   ```json
   {
     "ApiEndpoint": "https://hsngaming.de/api/api.php",
     "ApiKey": "yourapikey",
     "BanMethod": "kick"
   }
