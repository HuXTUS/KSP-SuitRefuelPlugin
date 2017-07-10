using System;
using System.Linq;
using KSP.Localization;
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
		
		readonly static int idEvaPropellant = "EVA Propellant".GetHashCode();
		static int idMonoPropellant = "MonoPropellant".GetHashCode();
		
		[KSPField(isPersistant = true)]
		public bool ledOn = false;

		GameObject gofr = null;

		Transform plug = null;
		
		readonly List<GameObject> listGofr = new List<GameObject>();
		
		[KSPEvent(guiActiveUnfocused = true, active = false, unfocusedRange = 35f, guiName = "Lol")]
		public void Lol()
		{
		}
		
		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 35f, guiName = "#SuRePl_Event_Settings")]
		public void ShowSettings()
		{
			ReadConfig();
			
			showSettings = !showSettings;
		}

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 1f, guiName = "#SuRePl_Event_Start_Refuel")]
		public void StartRefuelEvent()
		{

			UpdateEva();
			
			if (eva == null)
				return;
			
			if (!IsKerbalOnLadder()) {
				ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#SuRePl_Error_Ladder"), 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.Red;
				return;
			}

			Events["StartRefuelEvent"].active = false;
			Events["StopRefuelEvent"].active = true;
			
			refuelingState = RefuelingState.INIT;
			UpdateLEDs();
		}

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 1f, active = false, guiName = "#SuRePl_Event_Stop_Refuel")]
		public void StopRefuelEvent()
		{
			Events["StartRefuelEvent"].active = true;
			Events["StopRefuelEvent"].active = false;
			
			Events["ShowSettings"].active = true;
			
			refuelingState = RefuelingState.RECOVERY;
			
			UpdateLEDs();
		}
		
		
		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);

			UpdateEva();

			GameEvents.onVesselChange.Add(OnVesselChange);
			
			Events["ToggleEvent"].active = false;
			Events["ToggleEvent"].guiActive = false;
			Events["ToggleEvent"].guiActiveUncommand = false;
			Events["ToggleEvent"].guiActiveUnfocused = false;
			Actions["ToggleAction"].active = false;
			
		}
		
		public override void OnInactive()
		{
			GameEvents.onVesselChange.Remove(OnVesselChange);
		}
		
		
		void OnVesselChange(Vessel v)
		{
			showSettings = false;
			
			if (refuelingState != RefuelingState.NONE)
				StopRefuelEvent();
			//refuelingState = RefuelingState.RECOVERY;
			
			UpdateEva();
			
			UpdateLEDs();
		}
		
		void OnOff_LEDs(bool on)
		{

			if (ledOn != on) {
				ToggleEvent();
				ledOn = on;
			}
		}
		
		
		void UpdateLEDs()
		{

			if (eva) {
				OnOff_LEDs(true);
			} else {
				OnOff_LEDs(false);
			}

			UpdateLedRenderers();

		}
		
		bool _needUpdateLedRenderers = false;
		
		void UpdateLedRenderers()
		{
			_needUpdateLedRenderers = false;
			
			try {
				var rIdle = renderers.Find(x => x.gameObject.name.Equals("LedIdle"));
				rIdle.enabled = eva /*&& refuelingState == RefuelingState.NONE*/;
				
				var rProcess = renderers.Find(x => x.gameObject.name.Equals("LedProcess"));
				rProcess.enabled = refuelingState != RefuelingState.NONE;
				
				if (plug == null)
					plug = renderers.Find(x => x.gameObject.name.Equals("Plug")).gameObject.transform;
			} catch {
				_needUpdateLedRenderers = true;	
			}
			

		}
		
		void UpdateEva()
		{
		
			eva = null;
			
			var v = FlightGlobals.ActiveVessel;
			
			if (!v)
				return;
			
			if (!v.isEVA)
				return;
			
			eva = v;
			
			ReadConfig();
			
			ReadEvaResources();
			
			UpdateLEDs();

		}
		
		enum PumpingMode
		{
			NONE,
			IN,
			OUT
		}
		class ResourceExchangeSetting
		{
			public readonly PartResource res;
			public  PumpingMode pumping;
			public bool _stopped, _message;
			
			public ResourceExchangeSetting(PartResource r)
			{
				this.res = r;				
				if (r.resourceName.GetHashCode() == idEvaPropellant)
					pumping = PumpingMode.IN;
			}
			
		}
		
		readonly List<ResourceExchangeSetting> listSettings = new List<ResourceExchangeSetting>();
		
		void ReadEvaResources()
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
						s.pumping = (PumpingMode)Enum.Parse(typeof(PumpingMode), splitted[i + 1]);
				}
			}
			
		}

		bool _needCheckScene = true;
		
		public override void  FixedUpdate()
		{
			base.FixedUpdate();
			
			if (_needCheckScene) {
				_needCheckScene = false;
				
				if (HighLogic.LoadedSceneIsEditor)
					OnOff_LEDs(true);
				else
					UpdateLEDs();
				return;
			}
			
			if (_needUpdateLedRenderers) {
				Debug.Log("oh crap");
				UpdateLedRenderers();
			}

			if (refuelingState != RefuelingState.NONE) {
				ProcessRefuel();
			}

			if (eva == null)
				return;
		}

		const float GOFR_STEP = 0.028f;
		const float OK_DISTANCE_GOFR_TO_EVA = GOFR_STEP * 3f;
		
		void ProcessRefuel()
		{

			if ((eva != null) && !IsKerbalOnLadder() && refuelingState != RefuelingState.RECOVERY) {
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

				
				if (KerbalIsOutOfGofr()) {
					StopRefuelEvent();
					return;
				}

				float deltaTimeAmount = TimeWarp.fixedDeltaTime;

				bool stop = true;				
				
				foreach (var s in listSettings)
					if ((s.pumping != PumpingMode.NONE) && (!s._stopped)) {
					
						Part partFrom, partTo;
						if (s.pumping == PumpingMode.IN) {
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
								ShowRefuelEndedMessage(s.res.info.displayName, s.pumping, false);
							good = false;
						} else {
							idResource = (s.res.resourceName.GetHashCode() == idEvaPropellant) ? idEvaPropellant : idResource;
							float profit = partTo.RequestResource(idResource, -amount);
							
							if (-profit < amount) {
								if (s._message)
									ShowRefuelEndedMessage(s.res.info.displayName, s.pumping, true);
									
								good = false;
								
								idResource = (s.res.resourceName.GetHashCode() == idEvaPropellant) ? idMonoPropellant : idResource;
								partFrom.RequestResource(idResource, -amount - profit);
							}

							if (amount < (take / 10.0f)) {
								if (s._message)
									ShowRefuelEndedMessage(s.res.info.displayName, s.pumping, false);
								good = false;								
							}
							
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
					UpdateLEDs();
				}
				
			}
			
		}

		void ShowRefuelEndedMessage(string resname, PumpingMode pump, bool isFull)
		{

			if (isFull) {
				if (pump == PumpingMode.IN)
					ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#SuRePl_EVA_FULL") + " " + resname, 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.GreenYellow;
				else
					ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#SuRePl_SHIP_FULL") + " " + resname, 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.Orange;					
				
			} else {
				if (pump == PumpingMode.IN)
					ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#SuRePl_SHIP_OUT_OF") + " " + resname, 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.Red;
				else
					ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#SuRePl_EVA_OUT_OF") + " " + resname, 3.0f, ScreenMessageStyle.UPPER_CENTER).color = XKCDColors.Green;					
				
			}

		}

		bool IsKerbalOnLadder()
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
		
		bool KerbalIsOutOfGofr()
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
				windowPosition.xMin = 300;
				windowPosition.yMin = 300;
				
				windowPosition.height = 10;
			
				windowStyle.fixedWidth = 280;				
 
			}

			windowPosition = GUILayout.Window(0, windowPosition, OnWindowSettings, Localizer.GetStringByTag("#SuRePl_Caption_Settings"), windowStyle);
		}

		readonly Texture texResourceIn = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceIn", false);
		readonly Texture texResourceOut = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceOut", false);
		readonly Texture texResourceNothing = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceNothing", false);
		readonly Texture texResourceLockedIn = GameDatabase.Instance.GetTexture("SuitRefuelPlugin/Textures/resourceLockedIn", false);

		Texture TextureByResourceSetting(ResourceExchangeSetting s)
		{

			if (s.res.resourceName.GetHashCode() == idEvaPropellant)
				return texResourceLockedIn;			 
			
			if (s.pumping == PumpingMode.IN)
				return texResourceIn;
			if (s.pumping == PumpingMode.OUT)
				return texResourceOut; 

			return texResourceNothing;
		}
		
		void TogglePumpSetting(ResourceExchangeSetting s)
		{
			if (s.res.resourceName.GetHashCode() == idEvaPropellant)
				return;
			s._stopped = false;
			s._message = true;
			s.pumping++;
			if (s.pumping > PumpingMode.OUT)
				s.pumping = PumpingMode.NONE;
			
			SaveSettings();
		}
		
		public void OnWindowSettings(int windowId)
		{
			GUILayout.BeginVertical();
			
			foreach (var s in listSettings) {
				
				GUILayout.BeginHorizontal();
				
				if (GUILayout.Button(TextureByResourceSetting(s), HighLogic.Skin.box, GUILayout.Width(40)))
					TogglePumpSetting(s);
				if (GUILayout.Button(s.res.info.displayName, HighLogic.Skin.label))
					TogglePumpSetting(s);
				
				GUILayout.EndHorizontal();
			}

			if (GUILayout.Button(Localizer.GetStringByTag("#SuRePl_Hide_Settings"), HighLogic.Skin.button)) {
				showSettings = false;
			}
			
			GUILayout.EndVertical();			
			
			GUI.DragWindow();
			
		}
		
		void ReadConfig()
		{
			
			if (cfg != null)
				return;
			
			cfg = PluginConfiguration.CreateForType<SuitRefuel>();
			cfg.load();
		}
		
		void SaveSettings()
		{
			string pumpings = "";
			foreach (var s in listSettings)
				if (s.res.resourceName.GetHashCode() != idEvaPropellant)
				if (s.pumping != PumpingMode.NONE) {
					pumpings += s.res.resourceName + ":" + s.pumping.ToString() + ",";
				}
			
			cfg["pumpings"] = pumpings;
			cfg.save();
		}
		
	}
	
	
}
