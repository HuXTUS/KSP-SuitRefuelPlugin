using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

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
		
		Vessel eva = null;
		RefuelingState refuelingState = RefuelingState.NONE;
		
		readonly int idEvaPropellant = "EVA Propellant".GetHashCode();
		readonly int idMonoPropellant = "MonoPropellant".GetHashCode();
		
		[KSPField(isPersistant = true)]
		public bool ledOn = false;

		GameObject gofr = null;

		Transform plug = null;
		
		readonly List<GameObject> listGofr = new List<GameObject>();
		
		[KSPEvent(guiActiveUnfocused = true, active = false, unfocusedRange = 35f, guiName = "Lol")]
		public void Lol()
		{
		}

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 2f, guiName = "Refuel suit")]
		public void StartRefuelEvent()
		{
			
			updateEva();
			
			if (eva == null)
				return;
			
			if (eva.evaController.JetpackDeployed) {
				ScreenMessages.PostScreenMessage("Grab ladder and turn jetpack off!", 3.0f, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			Events["StartRefuelEvent"].active = false;
			Events["StopRefuelEvent"].active = true;
			
			refuelingState = RefuelingState.INIT;
			updateLEDs();
		}

		[KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 2f, active = false, guiName = "Stop refueling")]
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
			
			if (refuelingState != RefuelingState.NONE)
				refuelingState = RefuelingState.RECOVERY;
			
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
			
			updateLEDs();
			
		}

		public override void  FixedUpdate()
		{
			base.FixedUpdate();
			
			if (_needUpdateLedRenderers) {
				Debug.Log("oh crap");
				updateLedRenderers();
			}
			
			if (eva == null)
				return;
			
			if (refuelingState != RefuelingState.NONE) {
				processRefuel();
			}

	
		}

		void processRefuel()
		{
			
			if (eva.evaController.JetpackDeployed) {
				ScreenMessages.PostScreenMessage("Refueling cancelled", 3.0f, ScreenMessageStyle.UPPER_CENTER);
				StopRefuelEvent();
				
				return;
			}
			
			if (refuelingState == RefuelingState.INIT) {
				
				foreach (var element in listGofr)
					Destroy(element);				
			
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

				const float step = 0.03f;
				
				if (listGofr.Count < 10) {

					node.transform.rotation = gofr.transform.rotation;
					node.transform.position = gofr.transform.position + gofr.transform.forward * step;
				} else if (listGofr.Count < 30) {

					node.transform.position = gofr.transform.position + gofr.transform.forward * step;
					//0.027266f
					Vector3 targetDir = eva.transform.position - node.transform.position;
					Vector3 newDir = Vector3.RotateTowards(gofr.transform.forward, targetDir, 0.047266f, 0);
					node.transform.rotation = Quaternion.LookRotation(newDir);
					

				} else {
					
					node.transform.position = gofr.transform.position + gofr.transform.forward * step;
					
					Vector3 targetDir = eva.transform.position - node.transform.position;
					Vector3 newDir = Vector3.RotateTowards(gofr.transform.forward, targetDir, 0.087266f, 0);
					node.transform.rotation = Quaternion.LookRotation(newDir);
					
				}

				gofr = node;
				listGofr.Add(gofr);

				if ((listGofr.Count > 150) || ((listGofr.Count > 70) && (Vector3.Distance(gofr.transform.position, eva.transform.position) < step * 3))) {
					refuelingState = RefuelingState.REFUELING;
				}
				
			} else if (refuelingState == RefuelingState.REFUELING) {
				foreach (var element in eva.Parts) {
					foreach (var res in element.Resources) {
						if (res.resourceName.GetHashCode() == idEvaPropellant) {

							float deltaTimeAmount = TimeWarp.fixedDeltaTime;

							float amount = part.RequestResource(idMonoPropellant, deltaTimeAmount);

							if (amount <= part.resourceRequestRemainingThreshold) {
								ScreenMessages.PostScreenMessage("No MonoPropellant on ship, refueling stopped", 3.0f, ScreenMessageStyle.UPPER_CENTER);
								StopRefuelEvent();
								return;
							}

							float profit = element.RequestResource(idEvaPropellant, -amount);

							if (-profit < amount) {
								ScreenMessages.PostScreenMessage("Refueling complete", 3.0f, ScreenMessageStyle.UPPER_CENTER);
								StopRefuelEvent();
								return;
							}

						}
					
					} 
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
		
	}
}
