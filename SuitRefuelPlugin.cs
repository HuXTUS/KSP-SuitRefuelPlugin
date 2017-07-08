using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using KSP.IO;

namespace HuXTUS
{
	enum RefuelingState
	{
		NONE,
		INIT,
		UNTWISTING,
		REFUELING,
		RECOVERY
	}
	
	public class SuitRefuel : ModuleColorChanger
	{

		PluginConfiguration cfg;
 
		Vessel eva = null;
		RefuelingState refuelingState = RefuelingState.NONE;
		
		static int idEvaPropellant = "EVA Propellant".GetHashCode();
		static int idMonoPropellant = "MonoPropellant".GetHashCode();
		
		int puffIndex = -9;
		
		[KSPField(isPersistant = true)]
		public bool ledOn = false;

		GameObject gofr = null;

		Transform plug = null;
		
		readonly List<GameObject> listGofr = new List<GameObject>();
		
		[KSPEvent(guiActiveUnfocused = true, active = false, unfocusedRange = 35f, guiName = "Lol")]
		public void Lol()
		{

			foreach (var part in eva.Parts) {
				Debug.Log("part: ");
				foreach (var r in part.Resources) {

					
					Debug.Log(r.resourceName + " " + r.maxAmount.ToString() + " " + r.amount.ToString() + " " + r.GetInfo() + " " + r.info.displayName);
					
				}
				
			} 
			
		}
		
		
		[KSPEvent(guiActiveUnfocused = true, active = true, unfocusedRange = 35f, guiName = "Settings")]
		public void ShowSettings()
		{
			readConfig();
			
			showSettings = !showSettings;
		}
		
	

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 1f, guiName = "Refuel suit")]
		public void StartRefuelEvent()
		{

			updateEva();
			
			if (eva == null)
				return;
			
			if (!kerbalOnLadder()) {
				ScreenMessages.PostScreenMessage("Turn jetpack off and grab the ladder!", 3.0f, ScreenMessageStyle.UPPER_CENTER);
				return;
			}
			

			Events["StartRefuelEvent"].active = false;
			Events["StopRefuelEvent"].active = true;
			
			refuelingState = RefuelingState.INIT;
			updateLEDs();
		}

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 1f, active = false, guiName = "Stop refueling")]
		public void StopRefuelEvent()
		{
			Events["StartRefuelEvent"].active = true;
			Events["StopRefuelEvent"].active = false;
			
			refuelingState = RefuelingState.RECOVERY;
			
			updateLEDs();
		}
		
		
		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);

			updateEva();

			GameEvents.onVesselChange.Add(onVesselChange);
			
			Events["ToggleEvent"].active = false;
			Events["ToggleEvent"].guiActive = false;
			Events["ToggleEvent"].guiActiveUncommand = false;
			Events["ToggleEvent"].guiActiveUnfocused = false;
			Actions["ToggleAction"].active = false;
			
		}
		
		public override void OnInactive()
		{
			GameEvents.onVesselChange.Remove(onVesselChange);
		}
		
		
		void onVesselChange(Vessel v)
		{
			showSettings = false;
			
			if (refuelingState != RefuelingState.NONE)
				StopRefuelEvent();
			///refuelingState = RefuelingState.RECOVERY;
			
			updateEva();
			
			updateLEDs();
		}
		
		void OnOff_LEDs(bool on)
		{

			if (ledOn != on) {
				ToggleEvent();
				ledOn = on;
			}
		}
		
		
		void updateLEDs()
		{

			if (eva) {
				OnOff_LEDs(true);
			} else {
				OnOff_LEDs(false);
			}
		
			
			updateLedRenderers();
			
			
		}
		
		bool _needUpdateLedRenderers = false;
		
		void updateLedRenderers()
		{
			_needUpdateLedRenderers = false;
			
			try {
				var rIdle = renderers.Find(x => x.gameObject.name.Equals("LedIdle"));
				rIdle.enabled = eva && refuelingState == RefuelingState.NONE;
				
				var rProcess = renderers.Find(x => x.gameObject.name.Equals("LedProcess"));
				rProcess.enabled = refuelingState != RefuelingState.NONE;
				
				if (plug == null)
					plug = renderers.Find(x => x.gameObject.name.Equals("Plug")).gameObject.transform;
			} catch {
				_needUpdateLedRenderers = true;	
			}
			

		}
		
		void updateEva()
		{
		
			eva = null;
			
			var v = FlightGlobals.ActiveVessel;
			
			if (!v)
				return;
			
			if (!v.isEVA)
				return;
			
			eva = v;
			
			readConfig();
			
			readEvaResources();
			
			updateLEDs();
			
			
			
		}
		
		enum ResourcePumping
		{
			NONE,
			IN,
			OUT
		}
		class ResourceExchangeSetting
		{
			public readonly PartResource res;
			public  ResourcePumping pumping;
			public bool _stopped, _message;
			
			public ResourceExchangeSetting(PartResource r)
			{
				this.res = r;				
				if (r.resourceName.GetHashCode() == idEvaPropellant)
					pumping = ResourcePumping.IN;
			}
			
		}
		
		readonly List<ResourceExchangeSetting> listSettings = new List<ResourceExchangeSetting>();
		
		void readEvaResources()
		{
			
			if (listSettings.Any())
				return;
			
			listSettings.Clear();

			foreach (var r in eva.Parts[0].Resources) {
				listSettings.Add(new ResourceExchangeSetting(r));
			}

			string strPumpings = cfg.GetValue<string>("pumpings");
			if (strPumpings != null) {
				var splitted = strPumpings.Split(new string[] { ",", ":" }, StringSplitOptions.None);
				
				for (int i = 0; i < splitted.Count() - 1; i += 2) {
					var rname = splitted[i];
					var s = listSettings.Find(x => x.res.resourceName.Equals(rname));
					if (s != null)
						s.pumping = (ResourcePumping)Enum.Parse(typeof(ResourcePumping), splitted[i + 1]);
				}
			}
			
		}

		public override void  FixedUpdate()
		{
			base.FixedUpdate();
			
			if (_needUpdateLedRenderers) {
				Debug.Log("oh crap");
				updateLedRenderers();
			}

			if (refuelingState != RefuelingState.NONE) {
				processRefuel();
			}

			if (eva == null)
				return;
		}

		const float GOFR_STEP = 0.03f;
		const float OK_DISTANCE_GOFR_TO_EVA = GOFR_STEP * 3f;
		
		void processRefuel()
		{

			if ((eva != null) && eva.evaController.JetpackDeployed) {
				ScreenMessages.PostScreenMessage("Refueling cancelled", 3.0f, ScreenMessageStyle.UPPER_CENTER);
				StopRefuelEvent();
				
				return;
			}
			
			if (refuelingState == RefuelingState.INIT) {
				
				foreach (var element in listGofr)
					Destroy(element);

				foreach (var s in listSettings) {
					s._stopped = false;
					s._message = false;
				}
				
			
				listGofr.Clear();
			
				gofr = GameDatabase.Instance.GetModel("SuitRefuelPlugin/Parts/Gofr/model");
				gofr.SetActive(true);
				gofr.transform.parent = transform;
				//gofr.transform.localPosition = new Vector3(0, 0, 0);
				gofr.transform.position = plug.position;
				gofr.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
				gofr.transform.rotation = plug.rotation;
			
				listGofr.Add(gofr);
			
			
				refuelingState = RefuelingState.UNTWISTING;
				
			} else if (refuelingState == RefuelingState.UNTWISTING) {
				
				
				var node = Instantiate(gofr);
				node.SetActive(true);
				node.transform.parent = transform;
				node.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
				
				if (listGofr.Count < 5) {

					node.transform.rotation = gofr.transform.rotation;
					node.transform.position = gofr.transform.position + gofr.transform.forward * GOFR_STEP;
				} else if (listGofr.Count < 10) {

					node.transform.position = gofr.transform.position + gofr.transform.forward * GOFR_STEP;
					Vector3 targetDir = eva.transform.position - node.transform.position;
					Vector3 newDir = Vector3.RotateTowards(gofr.transform.forward, targetDir, 0.03f, 0);
					node.transform.rotation = Quaternion.LookRotation(newDir);
					
					
				} else if (listGofr.Count < 20) {

					node.transform.position = gofr.transform.position + gofr.transform.forward * GOFR_STEP;
					Vector3 targetDir = eva.transform.position - node.transform.position;
					Vector3 newDir = Vector3.RotateTowards(gofr.transform.forward, targetDir, 0.05f, 0);
					node.transform.rotation = Quaternion.LookRotation(newDir);
					

				} else {
					
					node.transform.position = gofr.transform.position + gofr.transform.forward * GOFR_STEP;
					
					Vector3 targetDir = eva.transform.position - node.transform.position;
					Vector3 newDir = Vector3.RotateTowards(gofr.transform.forward, targetDir, 0.06f, 0);
					node.transform.rotation = Quaternion.LookRotation(newDir);
					
				}

				gofr = node;
				listGofr.Add(gofr);

				if ((listGofr.Count > 150) || ((listGofr.Count > 70) && (Vector3.Distance(gofr.transform.position, eva.transform.position) < OK_DISTANCE_GOFR_TO_EVA))) {
					refuelingState = RefuelingState.REFUELING;
				}
				
			} else if (refuelingState == RefuelingState.REFUELING) {
				
				doPuff();
				
				if (!kerbalOnLadder()) {
					StopRefuelEvent();
					return;
				}
				
				if (kerbalIsOutOfGofr()) {
					StopRefuelEvent();
					return;
				}

				float deltaTimeAmount = TimeWarp.fixedDeltaTime;

				bool stop = true;				
				
				foreach (var s in listSettings)
					if ((s.pumping != ResourcePumping.NONE) && (!s._stopped)) {
					
						Part partFrom, partTo;
						if (s.pumping == ResourcePumping.IN) {
							partFrom = this.part;
							partTo = eva.Parts[0];						
						} else {
							partFrom = eva.Parts[0];
							partTo = this.part;
						}
						
						bool good = true;

						float take = (float)s.res.maxAmount / 5.0f * deltaTimeAmount;
						
						int idResource = (s.res.resourceName.GetHashCode() == idEvaPropellant) ? idMonoPropellant : s.res.resourceName.GetHashCode();
 
						float amount = partFrom.RequestResource(idResource, take);

						if (amount <= part.resourceRequestRemainingThreshold) {
							if (s._message)
								ScreenMessages.PostScreenMessage("Lack of " + s.res.info.displayName, 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.RedPurple;
							good = false;
						} else {
							idResource = (s.res.resourceName.GetHashCode() == idEvaPropellant) ? idEvaPropellant : idResource;
							float profit = partTo.RequestResource(idResource, -amount);
							
							if (-profit < amount) {
								if (s._message)
									ScreenMessages.PostScreenMessage(s.res.info.displayName + " completed", 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.GreenYellow;
								good = false;
								
								idResource = (s.res.resourceName.GetHashCode() == idEvaPropellant) ? idMonoPropellant : idResource;
								partFrom.RequestResource(idResource, -amount - profit);
							}

							if (amount < (take / 10.0f)) {
								if (s._message)
									ScreenMessages.PostScreenMessage("Lack of " + s.res.info.displayName, 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.RedPurple;
								good = false;								
							}

							Debug.Log(s.res.resourceName + " amount: " + amount.ToString() + "  profit: " + profit);							
							
						}
						
						if (!good)
							s._stopped = true;
						else {
							stop = false;
							s._message = true;
						}

					}
				
				if (stop) {
					StopRefuelEvent();
					return;	
				}
				
			} else if (refuelingState == RefuelingState.RECOVERY) {
				
				if (listGofr.Any()) {
					var g = listGofr.Last();
					Destroy(g);
					listGofr.Remove(g);
					
				} else {
					refuelingState = RefuelingState.NONE;
					updateLEDs();
				}
				
			}
			
		}
		
		//TODO: при переключении корабля очищать гофр, а то он торчит. А ещё start/stop обработать, а то название пункта меню портится
		
		void clearPuff()
		{
			foreach (var g in listGofr)
				g.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
		}
		
		
		void doPuff()
		{
			
			clearPuff();
			
			puffIndex++;
			if (puffIndex >= listGofr.Count)
				puffIndex -= listGofr.Count / 2;
			
			int currentIndex = puffIndex;
			
			do {
			
				for (int i = -5; i < 5; i++) {
				
					float newsize = 0.5f + ((8 - Math.Abs(i)) / 8f * 0.3f);
				
					int index = currentIndex + i;				

					if (index >= 0 && index < listGofr.Count)
						listGofr[index].transform.localScale = new Vector3(newsize, newsize, 0.5f);
				}
				
				
				currentIndex -= listGofr.Count / 2;
				
			} while (currentIndex > 0);
			
		}
		
		bool kerbalOnLadder()
		{
			
			if (eva == null)
				return false;
			if (eva.evaController == null)
				return false;
			if (eva.evaController.JetpackDeployed)
				return false;
			if (eva.evaController.LadderPart == null)
				return false;
			
			return this.name.Equals(eva.evaController.LadderPart.name);
			
		}
		
		bool kerbalIsOutOfGofr()
		{
			return !(Vector3.Distance(gofr.transform.position, eva.transform.position) < OK_DISTANCE_GOFR_TO_EVA);
		}
		
		
		bool showSettings = false;
		GUIStyle windowStyle = new GUIStyle(HighLogic.Skin.window);
		Rect windowPosition = new Rect(0, 0, 0, 0);
		public void OnGUI()
		{
			
			if (!showSettings)
				return;
			
			if ((windowPosition.xMin <= 1) && (windowPosition.yMin <= 1)) {	
				windowPosition.xMin = 500;
				windowPosition.yMin = 500;
				
				windowPosition.height = 10;
			
				windowStyle.fixedWidth = 250;				
			}

			windowPosition = GUILayout.Window(0, windowPosition, OnWindowSettings, "Suit Refuel Settings", windowStyle);
		}

		readonly Texture texResourceIn = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceIn", false);
		readonly Texture texResourceOut = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceOut", false);
		readonly Texture texResourceNothing = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceNothing", false);
		readonly Texture texResourceLockedIn = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceLockedIn", false);

		Texture textureByResourceSetting(ResourceExchangeSetting s)
		{

			if (s.res.resourceName.GetHashCode() == idEvaPropellant)
				return texResourceLockedIn;			 
			
			if (s.pumping == ResourcePumping.IN)
				return texResourceIn;
			if (s.pumping == ResourcePumping.OUT)
				return texResourceOut; 

			return texResourceNothing;
		}
		
		void togglePumpSetting(ResourceExchangeSetting s)
		{
			if (s.res.resourceName.GetHashCode() == idEvaPropellant)
				return;
			s._stopped = false;
			s._message = true;
			s.pumping++;
			if (s.pumping > ResourcePumping.OUT)
				s.pumping = ResourcePumping.NONE;
			
			saveSettings();
		}
		
		public void OnWindowSettings(int windowId)
		{
			GUILayout.BeginVertical();
			
			foreach (var s in listSettings) {
				
				GUILayout.BeginHorizontal();
				
				if (GUILayout.Button(textureByResourceSetting(s), HighLogic.Skin.box, GUILayout.Width(40)))
					togglePumpSetting(s);
				if (GUILayout.Button(s.res.info.displayName, HighLogic.Skin.label))
					togglePumpSetting(s);
				
				GUILayout.EndHorizontal();
			}

			if (GUILayout.Button("Hide", HighLogic.Skin.button)) {
				showSettings = false;
			}
			
			GUILayout.EndVertical();			
			
			GUI.DragWindow();
			
		}
		
		void readConfig()
		{
			
			if (cfg != null)
				return;
			
			cfg = PluginConfiguration.CreateForType<SuitRefuel>();
			cfg.load();
		}
		
		void saveSettings()
		{
			string pumpings = "";
			foreach (var s in listSettings)
				if (s.res.resourceName.GetHashCode() != idEvaPropellant)
				if (s.pumping != ResourcePumping.NONE) {
					pumpings += s.res.resourceName + ":" + s.pumping.ToString() + ",";
				}
			
			cfg["pumpings"] = pumpings;
			cfg.save();
		}
		
	}
	
	
}
