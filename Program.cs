
/*

Ideas:
	. Remote call with telemetry for other scripts to use.
	  Probably needs IGC to actually return a value.
 
*/

using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{
		// START_CLIP
		#region mdk preserve

		// === CONFIG ===

		// Print diagnostic info in echo panel.
		// (When you view the programmable block in the terminal.)
		// Also sets the [debug] display variable.
		static bool DebugMode = false;


		// Game constants - may be affected by other mods.

		// Game's maximum speed limit, usually 100 m/s.
		// If you are using speed mods, change this accordingly.
		const double HardSpeedLimit = 100.0;
		
		// Updates per second of game world time.
		// Not affected by sim speed slowdown.
		// Basically always 60.0
		const double UpdatesPerSecond = 60.0;



		// === AUTOPILOT LIMITS ===

		const double MaxSpeed = 95.0; // m/s
		const double MaxPitch = 30.0; // degrees
		const double MaxRoll = 45.0; // degrees
		const double MaxGyroSpeed = 60.0; // rpm


		// === TUNING ===

		// General adjustment for gyro sensitivity.
		// If the thing is developing wild oscillations, decrease this value.
		// If it's too sluggish, increase.
		// I suggest making changes in increments of no more than 0.1
		// at a time.
		double GeneralResponse = 1.0;
		double RollResponse = 1.0;
		double PitchResponse = 1.0;

		const double TurnRate = 15; // degrees per second to correct heading



		// === LANDING ===

		// When lining up with the runway
		const double LandingLineupToRunwaySpeed = 40;

		// How far out from the landing point to line up
		const double LandingFinalApproachDistance = 1000;

		// On final approach
		double LandingFinalPitch = 10;

		double LandingGlideSlope = 15; // degrees

		// Minimum speed the aircraft can fly.
		const double StallSpeed = 15; // m/s

		// Max speed on final landing approach.
		const double MaxFinalSpeed = 35;

		// It tends to come in a little low on the landing.
		// If your plane is big, or it's not hitting the target just right,
		// you can try adjusting this. Remember it's trying to put the
		// cockpit on target, not necessarily the rear landing gear.
		const double FinalGlideAltitudeOffset = 5.0;

		// When the plane is within this close to the landing point,
		// cut engines and engage parking brake.
		// Distance is measured horizontally, height doesn't matter.
		const double LandingArrivedDistance = 10.0; // m

		// === TARGETING ===

		// Note: Targeting operates on stationary coordinates. Does not track moving objects.

		// Master enable for targeting system
		bool enableTargetCam = true;

		// Name of the camera block to use for targeting.
		string TargetDesignatorCam = "Camera";
		//string TargetDesignatorCam = "Camera - Front";

		// Continue acquiring target after flyover?
		// Can now be set via the "loop" or "once" argument.
		bool enableLoopBackToTarget = true;

		// After passing the target, travel this far away before turning back towards it.
		// Can now be set via the "loop" argument
		double TargetLoopBackDistance = 2000;

		// Dumb gravity bombs
		bool BombingEnabled = true;
		// List of blocks to trigger when drop command is given
		string[] BombPylons =
		{
			"Merge Block - Left 1",
			"Merge Block - Right 1",
			"Merge Block - Left 2",
			"Merge Block - Right 2",
			"Merge Block - Left 3",
			"Merge Block - Right 3",
		};

		// Terminal action to perform on bomb pylon blocks.
		string BombDropAction = "OnOff_Off";

		// Time between bomb releases for "drop all" command.
		double BombDropDelay = 0.5; // seconds



		// ROUTING AND WAYPOINT NOTIFICATION

		// Timer block template
		//string On_NextWaypoint_Block = "Timer Block";
		//string On_NextWaypoint_Action = "TriggerNow";
		//string On_OneWayRouteComplete_Block = "Timer Block 2";
		//string On_OneWayRouteComplete_Action = "TriggerNow";

		// Sound block template
		string On_NextWaypoint_Block = "Sound Block Next Waypoint";
		string On_NextWaypoint_Action = "PlaySound";
		string On_OneWayRouteComplete_Block = "Sound Block Route End";
		string On_OneWayRouteComplete_Action = "PlaySound";



		// DISPLAY

		// Enable LCD / cockpit panel information
		bool enableDisplay = true;

		// List of blocks to use for display.
		// Block's Custom Data controls what is shown. See below.
		string[] Displays =
		{
			"Fighter Cockpit",
			//"LCD Panel"
		};

/*********************************
In the Custom Data of each display, you can configure what you want to show.
The script will scan the text for certain variables in square brackets []
and substitute the corresponding values.
For blocks with more than one panel, like cockpits or buttons,
put a line before your text that goes like this:
panel 0

Here is an example setup for a fighter cockpit.
Try pasting this into the cockpit's custom data:

---------------------------------------------------
panel 1
[mode]
[ttt] [target]
Bomb [bombtime]
[loopmode] [loopdistance]

panel 2
H [hdg] / [hdggoal]
A [alt] / [altgoal]
[altmode]

panel 3
[spd] / [spdgoal] m/s  [thrust]% thrust
P [pitch] / [pitchgoal]
R [roll] / [rollgoal]
---------------------------------------------------


List of available variables:
		
[status]   Mode, target, and waypoint info. Length can vary greatly.
[mode]     Just the mode, AUTO, MANUAL, TARGET, LAND, etc
[altmode]  Altitude mode. "Sealevel" or "Surface". 
[alt]      Current altitude
[altgoal]  Altitude ship is trying to achieve
[hdg]      Current heading
[hdggoal]  Heading ship is trying to achieve
[spd]      Current speed
[spdgoal]  Speed the ship is trying to achieve
[gps]      Current gps coordinates in lat/lon
		   Not to be confused with Space Engineers' cartesian coordinates
[lat]      Current latitude
[lon]      Current longitude
[target]   Name of current target (or waypoint)
[loopmode] Target loop back mode.
[loopdistance]  Distance to pass target before looping back.
[dtt]      Distance to target (or waypoint)
[hdtt]     Horizontal distance to target (or waypoint)
[cpu]      Percent of allowed cpu usage.
[hot]      Height above target
[ttt]      Time to target
[bombtime] count down to bombs away
           This might not be 100% accurate, but it gets you in the ballpark.
           Test it out on some dummy targets to get a feel for it.
[radius]   Radius of circle pattern used for target circle mode.
[roll]     Current bank angle
[rollgoal] Desired bank angle
[pitch]    Current pitch angle
[pitchgoal] Desired pitch angle
[sideslip] Angle between front of ship and velocity in the horizontal plane.
[aoa]      Angle of attack
[thrust]   Current percent thrust override.
[bombcount] Remaining bombs. (use the "reloaded" command to reset)
[localizer] Name of most recent runway localizer.

[pitchresponse]  Your manual adjustment to pitch response
[rollresponse]   Your manual adjustment to roll response

[debug]    Debugging messages.
           Debug mode must be turned on.




		// === END MAIN CONFIG ===

		// PID TUNING (Advanced)

		// PID constants are now determined dynamically based on the
		// "...Response" variable above. Any changes to specific PID constants,
		// should that be necessary, should be made in
		// ReconfigurePIDs(), not in their declarations.

		***********************************/

		void ReconfigurePIDs()
		{
			// .Kp is proportional response
			// .Ki is integral response
			// .Kd is differential response, typically not used and set to 0
			
			// Thrust probably doesn't need dynamic adjustment
			// measures speed, controls thrust override.
			// percent thrust / (m/s)
			//thrustController.Kp = 0.5;
			//thrustController.Ki = 0.1;


			// Measures altitude, controls pitch setpoint
			// degrees / m
			altitudeController.Kp = 0.4 * AutoResponse * PitchResponse * GeneralResponse;
			altitudeController.Ki = 0.1 * AutoResponse * PitchResponse * GeneralResponse;


			// Measures pitch, controls gyro overrides in horizontal plane
			// rpm / degrees
			pitchController.Kp = 1.0 * AutoResponse * PitchResponse * GeneralResponse;
			pitchController.Ki = 0.1 * AutoResponse * PitchResponse * GeneralResponse;


			// measures heading, controls gyro overrides in vertical plane
			// rpm / degrees
			headingController.Kp = 0.2 * AutoResponse * RollResponse * GeneralResponse;
			headingController.Ki = 0.05 * AutoResponse * RollResponse * GeneralResponse;


			// Measures horizontal angle between velocity and center line of ship,
			// controls roll angle setpoint.
			// degrees / degrees
			sideSlipController.Kp = 5.0 * AutoResponse * RollResponse * GeneralResponse;
			sideSlipController.Ki = 0.1 * AutoResponse * RollResponse * GeneralResponse;


			// Measures roll angle, controls gyros
			// rpm / degrees
			rollController.Kp = 0.5 * AutoResponse * RollResponse * GeneralResponse;
			rollController.Ki = 0.01 * AutoResponse * RollResponse * GeneralResponse;


			// Measures distance to target, controls fine heading adjustment
			// degrees / m
			circleRadiusController.Kp = 0.1 * AutoResponse * RollResponse * GeneralResponse;
			circleRadiusController.Ki = 0.01 * AutoResponse * RollResponse * GeneralResponse;

		}

		#endregion



		// measures speed, controls thrust
		PID thrustController = new PID
		{
			Kp = 0.5,
			Ki = 0.1,
			Kd = 0, //0.1,
			lowPass = 1.0,
			limMin = 0,
			limMax = 1.0,
			limMinInt = 0,
			limMaxInt = 0.2,
			dt = 1.0 / UpdatesPerSecond
		};

		// measures altitude, controls pitch setpoint
		PID altitudeController = new PID
		{
			//Kp = 0.1,
			//Ki = 0.05,
			//Kd = 0.01,
			Kp = 0.5,
			Ki = 0.1,
			Kd = 0, //0.1,
			lowPass = 20.0,
			limMin = -MaxPitch, // -4000,
			limMax = MaxPitch, //20000,
			limMinInt = 0, //set dynamically //-TypicalAngleOfAttack, // -10,
			limMaxInt = 0, //set dynamically  //TypicalAngleOfAttack * 2, // 10
			dt = 1.0 / UpdatesPerSecond
		};


		// measures pitch, controls gyros (rpm)
		PID pitchController = new PID
		{
			//Kp = PitchResponse * 1.0,
			//Ki = PitchResponse * 0.1,
			Kp = 0,
			Ki = 0,
			Kd = 0, //PitchResponse * 0.1,
			lowPass = 1.0,
			limMin = -5, // rpm
			limMax = 5,
			limMinInt = -1.5, // -MaxGyroSpeed / 2.0,
			limMaxInt = 1.5, //MaxGyroSpeed / 2.0,
			dt = 1.0 / UpdatesPerSecond
		};

		// Dev note: Heading needs an external set point, because of how angles
		// wrap around. The PID will try to achieve a setpoint of 0, measuring the signed difference
		// between current and desired heading, which will also be calculated externally.
		double setHeading = 90;

		// measures heading, controls gyros
		PID headingController = new PID
		{
			Kp = 0.2,
			Ki = 0.1,
			Kd = 0, // 0.05,
			lowPass = 1.0,
			limMin = -TurnRate * 0.1666666667, // degrees per second --> rpm
			limMax = TurnRate * 0.1666666667,
			limMinInt = -1.0, //-MaxGyroSpeed / 2.0,
			limMaxInt = 1.0, //MaxGyroSpeed / 2.0,
			dt = 1.0 / UpdatesPerSecond
		};

		// Measures horizontal angle between velocity and center line of ship,
		// controls roll angle setpoint
		PID sideSlipController = new PID
		{
			Kp = 0, // RollResponse * 5.0,
			Ki = 0, // RollResponse * .05,
			Kd = 0, // RollResponse * 1.0,
			lowPass = MaxRoll,
			limMin = -MaxRoll,
			limMax = MaxRoll,
			limMinInt = -1, // -MaxRoll / 2,
			limMaxInt = 1, //MaxRoll / 2,
			dt = 1.0 / UpdatesPerSecond
		};

		// measures roll, controls gyros (rpm)
		PID rollController = new PID
		{
			Kp = 0, // 0.5 * RollResponse,
			Ki = 0, //0.01 * RollResponse,
			Kd = 0, //0.01 * RollResponse,
			lowPass = MaxGyroSpeed, // 10
			limMin = -MaxGyroSpeed,
			limMax = MaxGyroSpeed,
			limMinInt = -MaxGyroSpeed / 2.0,
			limMaxInt = MaxGyroSpeed / 2.0,
			dt = 1.0 / UpdatesPerSecond
		};

		// measures distance to target, controls fine heading adjustment
		PID circleRadiusController = new PID
		{
			Kp = 0, // 0.1 * RollResponse,
			Ki = 0, //0.01 * RollResponse,
			Kd = 0.00,
			lowPass = 45,
			limMin = -45,
			limMax = 45,
			limMinInt = -5,
			limMaxInt = 5,
			dt = 1.0 / UpdatesPerSecond
		};


		// Measures deviation from landing approach line
		// in degrees of heading.
		// Controls fine heading adjustment
		PID ilsFineHeadingController = new PID
		{
			Kp = 3.0,
			Ki = 0.01,
			Kd = 0,
			lowPass = 10,
			limMin = -45,
			limMax = 45,
			limMinInt = -1,
			limMaxInt = 1,
			dt = 1.0 / UpdatesPerSecond
		};

		// Measures altitude deviation, controls thrust
		PID ilsSpeedController = new PID
		{
			Kp = 1.0,
			Ki = 0.5,
			Kd = 0,
			lowPass = 10,
			limMin = StallSpeed,
			limMax = MaxFinalSpeed,
			limMinInt = 0,
			limMaxInt = 10.0,
			dt = 1.0 / UpdatesPerSecond
		};
		
		//PID ilsPitchController = new PID
		//{
		//	Kp = 0.1,
		//	Ki = 0.01,
		//	Kd = 0,
		//	lowPass = 10,
		//	limMin = 10,
		//	limMax = MaxPitch,
		//	limMinInt = -2,
		//	limMaxInt = 2,
		//	dt = 1.0 / UpdatesPerSecond
		//};








		/*
		struct Display
		{
			public Display(string block_name, int panel_index) //, string p_defaultText)
			{
				block = block_name;
				panel = panel_index;
				//defaultText = p_defaultText;
			}
			public string block;
			public int panel;
			//public string defaultText;
		}
		*/

		IMyGridTerminalSystem G = null;
		IMyShipController activeCockpit = null;
		List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
		IMyRemoteControl rc;
		MatrixD shipWorld;
		Vector3D planetWorldPos, shipWorldPos, shipPlanetPos, planetDown, planetUp;
		double lat = 0, lon = 0;
		double altitude, heading, pitch, roll, sideslip;
		public static Program prg;

		//static readonly Vector3D posY = new Vector3D(0, 1, 0);
		static readonly Vector3D negY = new Vector3D(0, -1, 0);
		//static readonly Vector3D posX = new Vector3D(1, 0, 0);
		//static readonly Vector3D negX = new Vector3D(-1, 0, 0);
		//static readonly Vector3D posZ = new Vector3D(0, 0, 1);
		//static readonly Vector3D negZ = new Vector3D(0, 0, -1);
		static readonly string newline = "\r\n";
		bool autopilotOn = false;
		bool pastTarget = false;
		MyPlanetElevation AltitudeMode = MyPlanetElevation.Sealevel;

		bool waypointMode = false;
		string currentRemoteControlBlockName;
		int nextWaypointIndex = 0;
		List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();

		bool circleMode = false;
		double circleRadius = 1000;
		int circleDirection = 1; // 1 or -1

		bool ilsMode = false;
		bool ilsFinal = false;
		Vector3D landingTarget = new Vector3D();
		Vector3D beginFinalApproachPoint = new Vector3D();
		

		string status = "";
		string navmode = "";
		string llstr = "";

		#region mdk preserve
		// *Keyword vars have been deprecated and no longer affects everywhere "spd", "alt" and "hdg" can appear
		#endregion
		const string SpeedKeyword = "spd";
		const string AltitudeKeyword = "alt";
		const string HeadingKeyword = "hdg";

		int cpu;

		bool bombDropSequenceRunning = false;
		DateTime lastBombDropTime;
		int bombDropIndex = 0;

		static StringBuilder DebugText = new StringBuilder();

		public static void dbg(string s)
		{
			if (DebugMode)
			{
				prg.Echo(s);
				DebugText.Append(s);
				DebugText.Append(newline);
			}
		}
		public static void dbg(DeferredEvaluator<string> d)
		{
			if (DebugMode)
			{
				dbg(d());
			}
		}

		IMyBroadcastListener ilsListener;

		public Program()
		{
			G = GridTerminalSystem;
			prg = this;
			PID.p = this;
			altitudeController.setpoint = 1000;
			thrustController.setpoint = Clamp(50, 0, MaxSpeed);
			if (enableTargetCam)
			{
				var cam = G.GetBlockWithName(TargetDesignatorCam) as IMyCameraBlock;
				if (cam != null)
				{
					cam.EnableRaycast = true;
				}
				else
				{
					Echo($"Camera \"{TargetDesignatorCam}\" not found");
					enableTargetCam = false;
					Echo("TARGETING DISABLED");
				}
			}
			if (BombingEnabled)
			{
				foreach (var b in BombPylons)
				{
					if (G.GetBlockWithName(b) == null)
					{
						Echo($"Pylon \"{b}\" not found");
						BombingEnabled = false;
					}
				}
				if (BombingEnabled == false) { Echo("BOMBING DISABLED"); }
			}

			foreach (var d in Displays)
			{
				if (G.GetBlockWithName(d) == null)
				{
					Echo($"Display \"{d}\" not found");
				}
			}

			if (G.GetBlockWithName(On_NextWaypoint_Block) == null)
			{
				Echo($"On_NextWaypoint_Block \"{On_NextWaypoint_Block}\" not found");
			}
			if (G.GetBlockWithName(On_OneWayRouteComplete_Block) == null)
			{
				Echo($"On_OneWayRouteComplete_Block \"{On_OneWayRouteComplete_Block}\" not found");
			}

			ilsListener = IGC.RegisterBroadcastListener("ILS");

			Echo("\r\nReady.\r\n \r\n");
		}

		public void Save()
		{
		}

		public void Main(string argument, UpdateType updateSource)
		{
			if (argument == "dfp")
			{
				Runtime.UpdateFrequency = UpdateFrequency.None;

				dfp_test();
				//(G.GetBlockWithName("Atmospheric Thruster 3") as IMyThrust).ThrustOverridePercentage = 1.0f;

				//UpdateNav();
				//Echo(shipPlanetPos.ToString());
				return;
			}
			switch (updateSource)
			{
				case UpdateType.Update1:
				case UpdateType.Update10:
				case UpdateType.Update100:
					try
					{
						RunFrame();
					}
					catch (Exception e)
					{
						Popup(e.Message + e.StackTrace);
						Echo(e.Message + e.StackTrace);
					}
					break;
				default:
					ilsMode = false;
					ilsFinal = false;
					if (argument.ToLower().Trim() == "stop")
					{
						Stop();
						// in case something is targeted without restarting autopilot, these default values will be overwritten with current nav data
						altitudeController.setpoint = 0;
						thrustController.setpoint = 0;
						return;
					}
					if (!ParseArgs(argument))
					{
						//Echo("Error, bad argument.");
						Popup("Error, bad argument.");
						//Stop();
						return;
					}
					FindBlocks();
					Runtime.UpdateFrequency = UpdateFrequency.Update1;
					break;
			}
			cpu = (int)(Math.Round((float)Runtime.CurrentInstructionCount / (float)Runtime.MaxInstructionCount) * 100.0f);
		}

		void Stop()
		{
			Runtime.UpdateFrequency = UpdateFrequency.None;
			//FindBlocks();
			autopilotOn = false;
			pastTarget = false;
			ilsMode = false;
			ilsFinal = false;
			landAtGridName = "";
			useAnyRunway = false;
			target = null;
			FindThrust();
			foreach (var t in forwardThrusters)
			{
				try { t.ThrustOverride = 0; t.ThrustOverridePercentage = 0; } catch { }
			}
			
			FindGyros(GetCockpit());
			foreach (var g in gyros)
			{
				try
				{
					g.Roll = 0;
					g.Pitch = 0;
					g.Yaw = 0;
					g.GyroOverride = false;
				} catch { }
			}

			//if (enableDisplay)
			//{
			//	try
			//	{
			//		StartDisp();
			//		Disp("NAV OFF");
			//		EndDisp();
			//	}
			//	catch
			//	{
			//		Echo("Stopped.");
			//	};
			//}
			//else { Echo("Stopped."); }
			navmode = "NAV OFF";
			status = "NAV OFF";
			RunDisplays();
			Echo("Stopped.");
		}

		int posmod(int x, int m)
		{
			return (x % m + m) % m;
		}
		bool ParseArgs(string cmd)
		{
			string rest;

			try
			{
				var sp = cmd.Split(',');
				if (cmd == "")
				{
					autopilotOn = !autopilotOn;
					if (autopilotOn)
					{
						FindBlocks();
						UpdateNav();
						altitudeController.setpoint = altitude;
						setHeading = angmod(heading);
						thrustController.setpoint = Clamp(activeCockpit.GetShipSpeed(), 0, MaxSpeed);
						Runtime.UpdateFrequency = UpdateFrequency.Update1;
					}
					else
					{
						Stop();
					}
					return true;
				}
				else if (cmd == "nav" || cmd == "manual")
				{
					Stop();
					Runtime.UpdateFrequency = UpdateFrequency.Update1;
					autopilotOn = false;
					return true;
				}
				else if (cmd.StartsWith("target"))
				{
					//dbg("'target'");
					rest = cmd.Substring(6).Trim();
					if (rest.StartsWith("once"))
					{
						//dbg("'once'");
						rest = rest.Substring(4).Trim();
						circleMode = false;
						if (rest.Length > 0)
						{
							Popup("E once leftover");
							return false;
						}
						enableLoopBackToTarget = false;
					}
					else if (rest.StartsWith("loop"))
					{
						rest = rest.Substring(4).Trim();
						circleMode = false;
						if (rest.Length > 0)
						{
							TargetLoopBackDistance = double.Parse(rest);
							rest = "";
						}
						enableLoopBackToTarget = true;
					}
					else if (rest.StartsWith("cancel"))
					{
						target = null;
						circleMode = false;
						return true;
					}
					else if (rest.StartsWith("circle"))
					{
						rest = rest.Substring(6).Trim();
						circleMode = true;
						if (rest.StartsWith("left"))
						{
							rest = rest.Substring(4).Trim();
							circleDirection = 1;
							// fall through
						}
						else if (rest.StartsWith("right"))
						{
							rest = rest.Substring(5).Trim();
							circleDirection = -1;
							// fall through
						}

						if (rest.Length == 0)
						{
							rest = "";
							circleRadius = double.Parse(rest);
						}
					} // target circle
					else if (rest.StartsWith("land"))
					{
						// target land
						DesignateTarget();
						if (target != null && target.Value.HitPosition.HasValue)
						{
							UpdateNav();
							Land(navMain, target.Value.HitPosition.Value, shipWorldPos, CloseOrFar.CLOSE);
						}
						return true;
					}// target land

					// Put additional target subcommands before this
					else if (rest.Length > 0)
					{
						//dbg("E leftover");
						Popup("E leftover\r\n" + rest);
						return false;
					}
					//dbg("DesignateTarget()");
					DesignateTarget();
					return true;
				} // target
				else if (cmd.StartsWith("radius"))
				{
					ncmd(cmd, 6, ref circleRadius);
					circleRadiusController.setpoint = circleRadius;
					return true;
				}
				else if (cmd.StartsWith("loop"))
 				{
					rest = cmd.Substring(4);
					TargetLoopBackDistance = double.Parse(rest);
					Popup($"LOOP {TargetLoopBackDistance:0}", 1);
					return true;
				}
				else if (cmd.StartsWith("route"))
				{
					rest = cmd.Substring(5).Trim();
					if (rest.StartsWith("clear"))
					{
						nextWaypointIndex = 0;
						routeDirection = 1;
						waypointMode = false;
						return true;
					}
					else if (rest.StartsWith("next"))
					{
						//nextWaypointIndex = posmod(nextWaypointIndex + 1, waypoints.Count);
						NextWaypoint();
					}
					else if (rest.StartsWith("prev"))
					{
						//nextWaypointIndex = posmod(nextWaypointIndex - 1, waypoints.Count);
						PrevWaypoint();
					}
					else
					{
						waypointMode = !waypointMode;
					}
					if (waypointMode)
					{
						autopilotOn = true;
						ResumeRoute();
					}
					return true;
				}
				else if (cmd == "test next waypoint")
				{
					NotifyNextWaypoint();
					return true;
				}
				else if (cmd == "test route end")
				{
					NotifyRouteEnd();
					return true;
				}
				else if (cmd.StartsWith("sea"))
				{
					AltitudeMode = MyPlanetElevation.Sealevel;
					return true;
				}
				else if (cmd.StartsWith("surf"))
				{
					AltitudeMode = MyPlanetElevation.Surface;
					return true;
				}
				else if (cmd == "altmode")
				{
					if (AltitudeMode == MyPlanetElevation.Sealevel)
					{
						AltitudeMode = MyPlanetElevation.Surface;
					}
					else AltitudeMode = MyPlanetElevation.Sealevel;
					return true;
				}
				else if (cmd.StartsWith("dropdelay"))
				{
					ncmd(cmd, 9, ref BombDropDelay);
					//rest = cmd.Substring(9).Trim();
					//BombDropDelay = double.Parse(rest);
					//return true;
				}
				else if (cmd.StartsWith("drop"))
				{
					rest = cmd.Substring(4).Trim();
					if (rest.StartsWith("delay"))
					{
						rest = cmd.Substring(9).Trim();
						BombDropDelay = double.Parse(rest);
					}
					else if (rest.StartsWith("all"))
					{
						bombDropSequenceRunning = true;
						Runtime.UpdateFrequency = UpdateFrequency.Update1;
						DropNextBomb();
					}
					else
					{
						DropNextBomb();
					}
					return true;
				}
				else if (cmd == "reloaded")
				{
					bombDropIndex = 0;
					return true;
				}
				else if (cmd.StartsWith("land"))
				{
					rest = cmd.Substring(4).Trim();
					if (rest.Length > 0)
					{
						landAtGridName = rest;
						Popup(landAtGridName);
					}
					else
					{
						useAnyRunway = true;
						Popup("Any runway");
					}
					return true;
				}
				else if (cmd.StartsWith("response"))
				{
					ncmd(cmd, 8, ref GeneralResponse);
				}
				else if (cmd.StartsWith("pitchresponse"))
				{
					ncmd(cmd, 13, ref PitchResponse);
				}
				else if (cmd.StartsWith("rollresponse"))
				{
					ncmd(cmd, 12, ref RollResponse);
				}
				else if (cmd.StartsWith(SpeedKeyword))
				{
					ncmd(cmd, SpeedKeyword.Length, ref thrustController.setpoint);
					thrustController.setpoint = Clamp(thrustController.setpoint, 0, MaxSpeed);
					autopilotOn = true;
					return true;
				}
				else if (cmd.StartsWith(AltitudeKeyword))
				{
					ncmd(cmd, AltitudeKeyword.Length, ref altitudeController.setpoint);
					autopilotOn = true;
					return true;
				}
				else if (cmd.StartsWith(HeadingKeyword))
				{
					ncmd(cmd, HeadingKeyword.Length, ref setHeading);
					setHeading = angmod(setHeading);
					autopilotOn = true;
					return true;
				}
				else if (sp.Length == 3)
				{
					//headingController.setpoint = double.Parse(sp[0]);
					setHeading = angmod(double.Parse(sp[0]));
					altitudeController.setpoint = double.Parse(sp[1]);
					thrustController.setpoint = Clamp(double.Parse(sp[2]), 0, MaxSpeed);
					autopilotOn = true;
					//return true;
				}
				else
				{
					return false;
				}
				return true;
			}
			catch (Exception e)
			{
				Popup(e.Message + e.StackTrace);
				dbg(e.Message + e.StackTrace);
				Stop();
				return true;
			}
		}

		// Number command
		void ncmd(string cmd, int substr_start, ref double value)
		{
			string rest = cmd.Substring(substr_start).Trim();
			if (rest.StartsWith("="))
			{
				rest = rest.Substring(1).Trim();
				value = double.Parse(rest);
			}
			else if (rest.StartsWith("+"))
			{
				rest = rest.Substring(1).Trim();
				value += double.Parse(rest);
			}
			else if (rest.StartsWith("-"))
			{
				rest = rest.Substring(1).Trim();
				value -= double.Parse(rest);
			}
			else
			{
				value = double.Parse(rest);
			}
		}

		static double Clamp(double val, double min, double max)
		{
			if (val > max) { return max; }
			if (val < min) { return min; }
			return val;
		}

		static double angmod(double angle, double mod = 360.0)
		{
			while (angle < 0) { angle += mod; }
			while (angle >= mod) { angle -= mod; }
			return angle;
		}

		NavInfo
			ilsToApproach = new NavInfo(),
			targetNav = new NavInfo(),
			wayco = new NavInfo(),
			toWaypoint = new NavInfo(),
			ilsApproachToLandingLine = new NavInfo(),
			ilsShipToLanding = new NavInfo(),
			ilsLandingPoint = new NavInfo();

		double altsea;
		double altsurf;
		string landAtGridName = "";
		bool useAnyRunway = false;
		static readonly Vector3D zerovec = new Vector3D(0, 0, 0);
		string currentLocalizerName = "";
		DateTime lastLocalizerReceived = DateTime.Now - TimeSpan.FromSeconds(999);
		void RunFrame()
		{
			//if (GetCockpit() == null)
			FindBlocks();
			if (activeCockpit == null)
			{
				Popup("Error: No active cockpit.");
				return;
			}

			//if (enableDisplay)
			//{
			//	try { StartDisp(); }
			//	catch
			//	{
			//		enableDisplay = false;
			//		Echo("Display error");
			//	}
			//}


			// Check for nearby localizer
			while (ilsListener.HasPendingMessage)
			{
				//dbg("Pending message...");
				var ilsMsg = ilsListener.AcceptMessage();
				bool gotStart = false;
				bool gotEnd = false;
				bool gotGridName = false;
				Vector3D runwayStart = zerovec;
				Vector3D runwayEnd = zerovec;
				string gridName = "";
				foreach(var rawline in (ilsMsg.Data as string).Split(newlineSplitSeparator))
				{
					//dbg(rawline);
					var line = rawline.Trim().ToLower();
					var sp = line.Split('=');
					if (sp.Length == 2)
					{
						string[] vsp;
						switch (sp[0])
						{
							case "start":
								//dbg("start?");
								vsp = sp[1].Split(',');
								if (vsp.Length == 3)
								{
									try
									{
										runwayStart.X = double.Parse(vsp[0]);
										runwayStart.Y = double.Parse(vsp[1]);
										runwayStart.Z = double.Parse(vsp[2]);
										gotStart = true;
										//dbg("got Start");
									}
									catch (Exception e)
									{
										dbg(e.Message + e.StackTrace);
									}
								}
								break;
							case "end":
								//dbg("end?");
								vsp = sp[1].Split(',');
								if (vsp.Length == 3)
								{
									try
									{
										runwayEnd.X = double.Parse(vsp[0]);
										runwayEnd.Y = double.Parse(vsp[1]);
										runwayEnd.Z = double.Parse(vsp[2]);
										gotEnd = true;
										//dbg("got End");
									}
									catch (Exception e)
									{
										dbg(e.Message + e.StackTrace);
									}
								}
								break;
							case "gridname":
								gotGridName = true;
								gridName = sp[1].Trim();
								//dbg($"gridname {gridName}");
								break;
						}
					}
				}
				if (gotStart && gotEnd && gotGridName)
				{
					//dbg($"currentLocalizerName = {gridName}");
					currentLocalizerName = gridName;
					lastLocalizerReceived = DateTime.Now;
				}
				if (gotStart && gotEnd && gotGridName && (useAnyRunway || gridName.ToLower() == landAtGridName.ToLower()))
				{
					autopilotOn = true;
					if (ilsMode)
					{
						landingTarget = runwayStart;
						RecalcLanding(navMain, runwayStart, runwayEnd, CloseOrFar.FAR);
					}
					else
					{
						Land(navMain, runwayStart, runwayEnd, CloseOrFar.FAR);
					}
				}
			} //while (ilsListener.HasPendingMessage)


			if (lastLocalizerReceived < DateTime.Now - TimeSpan.FromSeconds(10)
				&& lastLocalizerReceived > DateTime.Now - TimeSpan.FromSeconds(11))
			{
				Popup("Left localizer range");
				currentLocalizerName = "";
			}



			// Navigation
			UpdateNav();
			var curSpeed = activeCockpit.GetShipSpeed();
			activeCockpit.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out altsea);
			activeCockpit.TryGetPlanetElevation(MyPlanetElevation.Surface, out altsurf);


			double curAoA = aoa(activeCockpit.GetShipVelocities().LinearVelocity, shipWorld);
			altitudeController.limMinInt = -curAoA * 2;
			altitudeController.limMaxInt = curAoA * 2;


			double headingAngleDiff = 0;

			try
			{
				if (bombDropSequenceRunning && DateTime.Now >= lastBombDropTime + TimeSpan.FromSeconds(BombDropDelay))
				{
					DropNextBomb();
				}
			}
			catch (Exception e)
			{
				dbg(e.Message + e.StackTrace);
				Popup(e.Message + e.StackTrace);
			}


			if (ilsMode)
			{
				if (ilsFinal)
				{
					dbg($"{(shipWorldPos - landingTarget).Length():0} m to land");
					if (horiz(shipWorldPos, landingTarget).Length() < LandingArrivedDistance)
					{
						activeCockpit.HandBrake = true;
						Stop();
					}
					//double distanceToApproachLineHoriz;
					//AngleBetweenDeg(beginFinalApproachPoint - landingTarget, shipWorldPos - landingTarget);
					ilsApproachToLandingLine.goTowards(beginFinalApproachPoint, landingTarget, planetWorldPos);
					ilsShipToLanding.goTowards(shipWorldPos, landingTarget, planetWorldPos);
					ilsFineHeadingController.setpoint = 0;
					ilsFineHeadingController.Update(ilsApproachToLandingLine.heading - ilsShipToLanding.heading);
					setHeading = angmod(ilsApproachToLandingLine.heading + ilsFineHeadingController.Output);


					ilsLandingPoint.goTowards(landingTarget, beginFinalApproachPoint, planetWorldPos);
					//double setalt = navMain.seaLevelAltitude (landingTarget - shipWorldPos).Length() * 
					dbg($"Land alt {ilsLandingPoint.seaLevelAltitude:0}");
					ilsSpeedController.setpoint = ilsLandingPoint.seaLevelAltitude + Math.Sin(LandingGlideSlope * Math.PI / 180.0) * (landingTarget - shipWorldPos).Length();
					ilsSpeedController.Update(navMain.seaLevelAltitude);
					thrustController.setpoint = ilsSpeedController.Output;

					altitudeController.setpoint = ilsSpeedController.setpoint + FinalGlideAltitudeOffset;

					dbg($"F.alt {navMain.seaLevelAltitude:0} / {ilsSpeedController.setpoint:0}");

					//
					//ilsPitchController.setpoint = LandingMinSpeed;
					//ilsPitchController.Update(curSpeed);

					//thrustController.setpoint = LandingFinalSpeed;
					//altitudeController.setpoint = ilsLandingPoint.seaLevelAltitude + Math.Sin(LandingGlideSlope) * (landingTarget - shipWorldPos).Length();
				}
				else
				{
					dbg($"{(beginFinalApproachPoint - shipWorldPos).Length():0} m to final < {LandingLineupToRunwaySpeed}");
					dbg($"alt {altsea:0} -> {ilsApproachToLandingLine.seaLevelAltitude:0}");

					if ((beginFinalApproachPoint - shipWorldPos).Length() < 100)
					{
						thrustController.setpoint = LandingLineupToRunwaySpeed - 5;
						ilsApproachToLandingLine.goTowards(beginFinalApproachPoint, landingTarget, planetWorldPos);
						//if (curSpeed < LandingLineupToRunwaySpeed && Math.Abs(heading - ilsApproachToLandingLine.heading) < 10)
						if (curSpeed < LandingLineupToRunwaySpeed)
						{
							navmode = "FINAL";
							ilsFinal = true;
							return;
						}
					}
					//ilsToApproach.init(shipWorldPos, beginFinalApproachPoint - shipWorldPos, planetWorldPos);
					ilsToApproach.goTowards(shipWorldPos, beginFinalApproachPoint, planetWorldPos);
					RecalcLanding(navMain, landingTarget, beginFinalApproachPoint, CloseOrFar.CLOSE);
					setHeading = ilsToApproach.heading;
				}
			}
			if (autopilotOn)
			//if (true) // For debugging
			{
				// Thrust
				thrustController.Update(curSpeed);
				if (autopilotOn)
				{
					foreach (var t in forwardThrusters)
					{
						t.ThrustOverridePercentage = (float)thrustController.Output;
					}
				}


				// Gyros
				//G.GetBlocksOfType(gyros)

				//pitchController.setpoint = (setHeading - hdg - 180.0) % 360.0 + 180.0;


				//// ^ desmos.com/calculator :
				////            90\ -\ \operatorname{abs}\left(\operatorname{mod}\left(s-x-90,\ 360\right)-180\right)
				//angleDiff =   90.0 -          Math.Abs(      angmod( (setHeading - heading - 90.0), 360.0) - 180.0);
				////angleDiff = 90.0 - Math.Abs(  /*mod*/ ((setHeading - hdg - 90.0) % 360.0) - 180.0);

				// Desmos:
				// 180\ -\ \operatorname{mod}\left(s-x-180,\ 360\right)
				headingAngleDiff = 180 - angmod(setHeading - heading - 180.0, 360);
				headingController.setpoint = 0;
				dbg($"headcnt update {headingAngleDiff:0}"); // out {headingController.Output:0.00}");
				headingController.Update(headingAngleDiff);

				sideSlipController.setpoint = 0;
				//dbg("--- sideSlipController ---");
				//sideSlipController.Update(sideslip, true);
				sideSlipController.Update(sideslip);

				

				//rollController.setpoint = Clamp(headingController.Output, -MaxRoll, MaxRoll);
				rollController.setpoint = sideSlipController.Output;
				rollController.Update(roll);



				altitudeController.Update(altitude);

				//pitchController.setpoint = Clamp(altitudeController.Output, -MaxPitch, MaxPitch);

				//pitchController.setpoint = ilsFinal
				//	? ilsPitchController.Output
				//	: Clamp(altitudeController.Output, -MaxPitch, MaxPitch);
				//pitchController.setpoint = Clamp(altitudeController.Output, -MaxPitch, MaxPitch);

				if (ilsFinal)
				{
					pitchController.setpoint = LandingFinalPitch;
				}
				else
				{
					pitchController.setpoint = ilsFinal ? 20 : Clamp(altitudeController.Output, -MaxPitch, MaxPitch);
				}
				pitchController.Update(pitch);

				var rollRad = roll * Math.PI / 180.0;
				var pitchRad = pitch * Math.PI / 180.0;
				var gpitch =(pitchController.Output * Math.Cos(rollRad)) + (headingController.Output * (Math.Sin(rollRad)));
				var gyaw = (pitchController.Output * -Math.Sin(rollRad)) + (headingController.Output * Math.Cos(rollRad));

				foreach (var gt in gyt)
				{
					gt.setRoll(gt.g, rollController.Output);
					gt.setYaw(gt.g, gyaw);
					gt.setPitch(gt.g, gpitch);
				}

				if (!ilsMode && target.HasValue && target.Value.HitPosition.HasValue)
				{
					var targetVec = target.Value.HitPosition.Value - shipWorldPos;
					targetNav.init(shipWorldPos, targetVec, planetWorldPos);
					//Disp($"TGT {loopmode} {target.Value.Name}");
					status = ($"TGT {loopmode()} {target.Value.Name}");
					navmode = "TARGET";

					if (circleMode)
					{
						double currentRadius = hdtt().Value;
						double radiusDiff = currentRadius - circleRadius;
						circleRadiusController.setpoint = circleRadius;
						circleRadiusController.Update(currentRadius);
						//if (radiusDiff > CircleTargetApproachDistance)
						//{
						//	dbg("Target circle: approach");
						//	setHeading = angmod(targetNav.heading);
						//}
						//else if (radiusDiff < -CircleTargetApproachDistance)
						//{
						//	dbg("Target circle: too close");
						//	setHeading = angmod(targetNav.heading + 180);
						//}
						//else
						//{
						//	double correction = (-circleDirection * radiusDiff * CircleRadiusSensitivity);
						//	dbg($"Target circle: {targetNav.heading:0} + {(circleDirection * 90.0):0} + {correction:0.0}");
						//	setHeading = angmod(targetNav.heading + (circleDirection * 90.0) + correction);
						//}
						setHeading = angmod(targetNav.heading + (circleDirection * (90.0 + circleRadiusController.Output)));
					} // if circleMode
					else
					{

						//navmode = "TARGET";
						
						if (!pastTarget && shipWorld.Forward.Dot(targetVec) < 0)
						{
							if (enableLoopBackToTarget) { pastTarget = true; }
							else { target = null; }
						}
						//else if (pastTarget && targetVec.Length() < TargetLoopBackDistance)
						else if (pastTarget && horiz(shipWorldPos, target.Value.HitPosition.Value).Length() < TargetLoopBackDistance)
						{
						}
						else
						{
							
							setHeading = angmod(targetNav.heading);
							autopilotOn = true;
						}
					} // if circleMode /else 
				}
				else if (!ilsMode && waypointMode && nextWaypointIndex >= 0 && nextWaypointIndex < waypoints.Count)
				{
					//nextWaypointIndex = (int)Clamp(nextWaypointIndex, 0, waypoints.Count);
					var waypoint = waypoints[nextWaypointIndex];
					//Disp($"NAV AUTO {waypoint.Name}");
					status = ($"AUTO {waypoint.Name}");
					navmode = "AUTO";
					wayco.init(waypoint.Coords, negY, planetWorldPos);
					if (Math.Abs(wayco.latitude - lat) < 0.1
						&& Math.Abs(wayco.longitude - lon) < 0.1)
						// 0.1 degrees == 105 m with a planet radius of 60km
					{
						NextWaypoint();
					}
					if (waypointMode) // Might have been canceled if one way trip
					{
						//nextWaypointIndex = posmod(nextWaypointIndex + 1, waypoints.Count);
						waypoint = waypoints[nextWaypointIndex];
						//toWaypoint = new NavInfo(shipWorldPos, waypoint.Coords - shipWorldPos, planetWorldPos);
						toWaypoint.init(shipWorldPos, waypoint.Coords - shipWorldPos, planetWorldPos);
						setHeading = angmod(toWaypoint.heading);
					}
				}
				else
				{
					navmode = ilsMode ? ilsFinal ? "FINAL" : "LAND" : "AUTO";
					status = navmode;
				}
			} // if autopilotOn
			else
			{
				status = "MANUAL";
				navmode = "MANUAL";
			}



			//Displn($"{SpeedKeyword} {Math.Round(curSpeed, 0)} / {Math.Round(thrustController.setpoint,0)}");

			//Displn($"Alt {Math.Round(altitude,0)} / {altitudeController.setpoint} / {Math.Round(altitudeController.Output,2)}");
			//Displn($"Alt {Math.Round(altitude,0)} / {altitudeController.setpoint}");

			//Displn($"{HeadingKeyword} {Math.Round(hdg, 1)} {SpeedKeyword} {Math.Round(curSpeed, 0)} / {Math.Round(thrustController.setpoint, 0)}");
			//Displn($"Pitch {Math.Round(pitch, 1)} Roll {Math.Round(roll, 1)}");

			//Displn($"Pitch {Math.Round(pitch, 1)} / {Math.Round(pitchController.setpoint,1)} / {Math.Round(pitchController.Output,2)}");
			//Displn($"Pitch {Math.Round(pitch, 1)} / {Math.Round(pitchController.setpoint,1)}");





			//Displn($"{HeadingKeyword} {Math.Round(hdg)} @ {Math.Round(altitude)} / {Math.Round(altitudeController.setpoint)}");
			//Displn($"Eng {Math.Round(thrustController.Output * 100.0, 0)}%");
			//Displn($"{HeadingKeyword} {Math.Round(hdg)} / {Math.Round(setHeading)} / {Math.Round(headingController.Output)}");
			//Displn($"angleDiff = {Math.Round(angleDiff, 2)}");
			//Displn($"hdiff {Math.Round(angleDiff, 1)} / rollTarget {headingController.Output}");
			//Displn($"Roll {roll:0.0} / {rollController.setpoint:0.0} / {rollController.Output:0.0} rpm");


			//Disp($"{HeadingKeyword} {heading:0} --> {setHeading:0}");
			//Disp($"{AltitudeKeyword} {altitude:0} --> {altitudeController.setpoint:0}");
			//Disp($"{SpeedKeyword} {curSpeed:0} --> {thrustController.setpoint:0}");

			llstr = "";
			if (lat > 0) { llstr += $"{lat:0.00} N x "; }
			else { llstr += $"{-lat:0.00} S x "; }
			if (lon > 0) { llstr += $"{lon:0.00} E"; }
			else { llstr += $"{-lon:0.00} W"; }
			//Displn(llstr);


			//if (enableDisplay)
			//{
			//	try { EndDisp(); }
			//	catch
			//	{
			//		Echo("Display error");
			//	}
			//}
			RunDisplays();

		} // RunFrame()

		//IMyTextSurface getDisplay()
		//{
		//	var blk = G.GetBlockWithName(displayBlock);
		//	if (blk is IMyTextSurface)
		//	{
		//		return blk as IMyTextSurface;
		//	}
		//	else if (blk is IMyTextSurfaceProvider)
		//	{
		//		var p = blk as IMyTextSurfaceProvider;
		//		return p.GetSurface(displayIndex);
		//	}
		//	return null;
		//}
		//StringBuilder dispContents = new StringBuilder();
		//IMyTextSurface dispSurface;
		//void StartDisp()
		//{
		//	dispSurface = getDisplay();
		//	if (dispSurface == null) { return; }
		//	dispSurface.ContentType = ContentType.TEXT_AND_IMAGE;
		//	dispContents.Clear();
		//	//dispContents.Append(dispSurface.GetText());
		//}
		//void EndDisp()
		//{
		//	if (DateTime.Now < popupExpire)
		//	{
		//		dispSurface.WriteText(popupMsg);
		//	}
		//	else
		//	{
		//		dispSurface.WriteText(dispContents);
		//	}
		//}
		//void Displn(string addText)
		//{
		//	dispContents.Append(addText);
		//	dispContents.Append(newline);
		//	Echo(addText);
		//}
		//void Disp(string addText, bool insertNewLine = true)
		//{
		//	dispContents.Append(addText);
		//	if (insertNewLine) { dispContents.Append(newline); }
		//	Echo(addText);
		//}

		int routeDirection = 1;

		void PrevWaypoint() { NextWaypoint(true); }

		void NextWaypoint(bool previous = false)
		{
			if (rc == null)
			{
				waypointMode = false;
				return;
			}
			waypoints.Clear();
			//if (rc.FlightMode != FlightMode.Patrol) { routeDirection = 1; }

			rc.GetWaypointInfo(waypoints);
			if (waypoints.Count == 1)
			{
				nextWaypointIndex = 0;
				return;
			}
			nextWaypointIndex += routeDirection * (previous ? -1 : 1);
			if (nextWaypointIndex >= waypoints.Count)
			{
				switch (rc.FlightMode)
				{
					case FlightMode.Circle:
						nextWaypointIndex = 0;
						break;
					case FlightMode.Patrol:
						routeDirection = -1;
						nextWaypointIndex = waypoints.Count - 2;
						break;
					case FlightMode.OneWay:
						try
						{
							target = new MyDetectedEntityInfo(0, waypoints[nextWaypointIndex].Name, MyDetectedEntityType.Planet, waypoints[nextWaypointIndex].Coords, new MatrixD(), new Vector3(), MyRelationsBetweenPlayerAndBlock.Neutral, new BoundingBoxD(), DateTime.Now.ToFileTime());
							circleMode = true;
						}
						catch { }
						waypointMode = false;
						nextWaypointIndex = 0;
						break;
				}
			}
			else if (nextWaypointIndex < 0)
			{
				routeDirection = 1;
				nextWaypointIndex = 1;
			}

			// Notify / event triggers
			if (waypointMode)
			{
				NotifyNextWaypoint();
			}
			else
			{
				NotifyRouteEnd();
				Popup("ROUTE END");
			}
		}

		void NotifyRouteEnd()
		{
			// route done notify
			var bdone = G.GetBlockWithName(On_OneWayRouteComplete_Block);
			if (bdone != null)
			{
				try { bdone.ApplyAction(On_OneWayRouteComplete_Action); }
				catch { }
			}
		}
		void NotifyNextWaypoint()
		{
			// Next waypoint notify
			var bnext = G.GetBlockWithName(On_NextWaypoint_Block);
			if (bnext != null)
			{
				try { bnext.ApplyAction(On_NextWaypoint_Action); }
				catch { }
			}
		}

		NavInfo navMain = new NavInfo();
		void UpdateNav()
		{
			shipWorld = activeCockpit.WorldMatrix;
			shipWorldPos = shipWorld.Translation;
			if (!activeCockpit.TryGetPlanetPosition(out planetWorldPos)) { throw new Exception("Not on a planet"); }
			shipPlanetPos = shipWorldPos - planetWorldPos;
			var facing = shipWorld.Forward;

			navMain.init(shipWorldPos, shipWorld.Forward, planetWorldPos);

			/****************************************************************
			planetDown = activeCockpit.GetNaturalGravity();
			planetDown.Normalize();
			lat = Math.Asin(-planetDown.Dot(negY)) * 180.0 / Math.PI;
			if (shipPlanetPos.X == 0.0) { shipPlanetPos.X = 0.0001; }
			lon = Math.Atan2(shipPlanetPos.Z, shipPlanetPos.X) * 180.0 / Math.PI;
			//if (shipPlanetPos.Y < 0 && shipPlanetPos.X < 0) { lon = -lon; }
			//if (!activeCockpit.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out elevation)) { throw new Exception("Not on a planet"); }
			if (!activeCockpit.TryGetPlanetElevation(AltitudeMode, out altitude)) { throw new Exception("Not on a planet"); }

			//var vels = activeCockpit.GetShipVelocities();
			var vel = activeCockpit.GetShipVelocities().LinearVelocity;
			//var facing = shipWorld.Forward;

			//bool faceVel = true;
			bool faceVel = false;
			if (faceVel) { facing = vel; }

			//Echo($"Facing {facing}");
			
			var east = negY.Cross(shipPlanetPos);
			east.Normalize();

			var planetUp = shipPlanetPos;
			planetUp.Normalize();
			var verticalFactor = planetUp.Dot(facing); // / shipPlanetPos.Length();
			var vertComponent = planetUp * verticalFactor;
			var horizontal = facing - vertComponent;

			//Disp($"mags {negY.Length()}, {east.Length()}, {facing.Length()}");
			//var aboveOrBelowEastWestPlane = negY.Dot(east.Cross(facing));
			var aboveOrBelowEastWestPlane = planetUp.Dot(east.Cross(facing));
			var atoe = AngleBetweenDeg(horizontal, east); // angle to east
			//Disp($"angle to east = {Math.Round(atoe, 1)}");
			//Disp($"ab { aboveOrBelowEastWestPlane}");
			if (aboveOrBelowEastWestPlane > 0)
			{
				//Disp("above");
				hdg = 90 - AngleBetweenDeg(horizontal, east);
			}
			else
			{
				//Disp("below");
				hdg = 90 + AngleBetweenDeg(horizontal, east);
			}

			while (hdg < 0) { hdg += 360.0; }
			while (hdg >= 360.0) { hdg -= 360.0; }

			pitch = Math.Asin(verticalFactor) * 180.0 / Math.PI;
			//roll = Math.Asin(planetUp.Dot(shipWorld.Right)) * 180 / Math.PI;
			*************************************************************************/

			heading = navMain.heading;
			pitch = navMain.pitch;
			lat = navMain.latitude;
			lon = navMain.longitude;
			
			//planetUp = shipWorldPos; planetUp.Normalize();
			planetUp = shipPlanetPos; planetUp.Normalize();
			planetDown = -planetUp;
			var levelRight = facing.Cross(planetUp);
			levelRight.Normalize();
			roll = Math.Acos(levelRight.Dot(shipWorld.Right)) * 180.0 / Math.PI;
			if (double.IsNaN(roll)) { roll = 0; } // Dot might be slightly > 1
			if (facing.Dot(levelRight.Cross(shipWorld.Right)) < 0) { roll *= -1.0; }
			activeCockpit.TryGetPlanetElevation(AltitudeMode, out altitude);
			var hvel = horiz(shipWorldPos, shipWorldPos + activeCockpit.GetShipVelocities().LinearVelocity);
			var hface = horiz(shipWorldPos, shipWorldPos + shipWorld.Forward);
			sideslip = AngleBetweenDeg(hvel, hface);
			//dbg($"|sideslip| = {sideslip:0.00}");
			var up = shipPlanetPos;
			up.Normalize();
			sideslip *= Math.Sign(up.Dot(hvel.Cross(hface)));
			//dbg($"sideslip = {sideslip:0.00}");

		}

		static double planetSeaLevelRadius = 0;
		IMyShipController GetCockpit()
		{
			var bl = new List<IMyShipController>();
			G.GetBlocksOfType(bl);
			foreach (var b in bl)
			{
				if (b.IsUnderControl)
				{
					activeCockpit = b;
					double elevation;
					Vector3D pp;
					bool posgood = b.TryGetPlanetPosition(out pp);
					bool elvgood = b.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out elevation);
					if (elvgood && posgood)
					{
						planetSeaLevelRadius = (b.WorldMatrix.Translation - pp).Length() - elevation;
						dbg($"planet sea radius {planetSeaLevelRadius:0}");
					}
					return b;
					//b.CubeGrid.GridSizeEnum == MyCubeSize.Large
					//b.CubeGrid.GridSizeEnum == MyCubeSize.Small
				}
			}
			return activeCockpit;
		}

		// PID code adapted to C# from https://github.com/pms67/PID
		class PID
		{
			public static Program p;

			/* Controller gains */
			public double Kp;
			public double Ki;
			public double Kd;

			/* Derivative low-pass filter time constant */
			public double lowPass;

			/* Output limits */
			public double limMin;
			public double limMax;

			/* Integrator limits */
			public double limMinInt;
			public double limMaxInt;

			/* Sample time (in seconds) */
			public double dt;

			public double setpoint;
			//public double lowPassOut;

			/* Controller "memory" */
			// TODO: make private again
			public double integrator;
			public double prevError;            /* Required for integrator */
			public double differentiator;
			public double prevMeasurement;      /* Required for differentiator */
			//private double prevOut;

			/* Controller output */
			private double m_output;
			public double Output { get { return m_output; } }

			public double Update(double measurement, bool debugPrint = false)
			{
				double error = setpoint - measurement;
				double proportional = Kp * error;
				//integrator = integrator + 0.5f * Ki * dt * (error + prevError);
				double integrator_delta = 0.5f * Ki * dt * (error + prevError);
				integrator += integrator_delta;
				//integrator = integrator + 0.5f * Ki * dt * (error + prevError);

				if (debugPrint)
				{
					dbg($"integrator += 0.5f * {Ki:0.00} * {dt:0.00} * ({error:0.00} + {prevError:0.00});");
					dbg($"integrator delta = {integrator_delta:0.000}");
					dbg($"check 1 integrator {integrator:0.00}");
				}

				/* Anti-wind-up via integrator clamping */
				if (integrator > limMaxInt)
				{
					if (debugPrint) { dbg($"limMaxInt = {limMaxInt:0.00}"); }
					if (debugPrint) { dbg($"check 1.1 integrator {integrator:0.00}"); }
					integrator = limMaxInt;
					if (debugPrint) { dbg($"check 1.2 integrator {integrator:0.00}"); }
				}
				else if (integrator < limMinInt)
				{
					if (debugPrint) { dbg($"limMinInt = {limMinInt:0.00}"); }
					if (debugPrint) { dbg($"check 1.3 integrator {integrator:0.00}"); }
					integrator = limMinInt;
					if (debugPrint) { dbg($"check 1.4 integrator {integrator:0.00}"); }
				}

				if (debugPrint) { dbg($"check 2 integrator {integrator:0.00}"); }


				// (band-limited differentiator)
				differentiator = -(2.0f * Kd * (measurement - prevMeasurement)   /* Note: derivative on measurement, therefore minus sign in front of equation! */
									+ (2.0f * lowPass - dt) * differentiator)
									/ (2.0f * lowPass + dt);


				//if (proportional == double.NaN) { throw new ArgumentException("proportional NaN"); }
				//if (integrator == double.NaN) { throw new ArgumentException("integrator NaN"); }
				//if (differentiator == double.NaN) { throw new ArgumentException("differentiator NaN"); }
				//if (proportional == double.NaN) { p.Disp("proportional NaN"); }
				//if (integrator == double.NaN) { p.Disp("integrator NaN"); }
				//if (differentiator == double.NaN) { p.Disp("differentiator NaN"); }

				// Compute output and apply limits
				m_output = proportional + integrator + differentiator;
				if (debugPrint)
				{
					dbg($"E {error:0.00} P {proportional:0.00} I {integrator:0.00} D {differentiator:0.00} Out {m_output:0.00}");
				}

				//if (m_output == double.NaN) { p.Disp("output NaN"); }
				if (m_output > limMax)
				{

					m_output = limMax;

				}
				else if (m_output < limMin)
				{

					m_output = limMin;

				}


				/* Store error and measurement for later use */
				prevError = error;
				prevMeasurement = measurement;

				if (double.IsNaN(integrator)) { integrator = 0; }
				if (double.IsNaN(prevError)) { prevError = 0; }
				if (double.IsNaN(differentiator)) { differentiator = 0; }
				if (double.IsNaN(prevMeasurement)) { prevMeasurement = 0; }
				if (double.IsNaN(m_output)) { m_output = 0; }


				/* Return controller output */
				return m_output;
			}
		} // class PID

		Vector3 mul(MatrixD m, Vector3 v)
		{
			return new Vector3(
				(m.M11 * v.X) + (m.M21 * v.Y) + (m.M31 * v.Z) + m.M41,
				(m.M12 * v.X) + (m.M22 * v.Y) + (m.M32 * v.Z) + m.M42,
				(m.M13 * v.X) + (m.M23 * v.Y) + (m.M33 * v.Z) + m.M43);
		}


		static double AngleBetweenDeg(Vector3D pa, Vector3D pb) //returns radians 
		{
			return (180.0 / Math.PI) * Math.Acos(pa.Dot(pb) / (pa.Length() * pb.Length()));
		}

		List<IMyThrust> forwardThrusters = new List<IMyThrust>();
		void FindThrust()
		{
			//shipWorld.Forward
			List<IMyThrust> allThrusters = new List<IMyThrust>();
			forwardThrusters.Clear();
			G.GetBlocksOfType(allThrusters);
			foreach (var t in allThrusters)
			{
				if (t.GridThrustDirection.Z > 0)
				{
					forwardThrusters.Add(t);
				}
			}
		}


		delegate double GyroOverrideGetter(IMyGyro g);
		delegate void GyroOverrideSetter(IMyGyro g, double rpm);
		static double gyroGetPosPitch(IMyGyro g) => g.Pitch;
		static double gyroGetNegPitch(IMyGyro g) => -g.Pitch;
		static double gyroGetPosRoll(IMyGyro g) => g.Roll;
		static double gyroGetNegRoll(IMyGyro g) => -g.Roll;
		static double gyroGetPosYaw(IMyGyro g) => g.Yaw;
		static double gyroGetNegYaw(IMyGyro g) => -g.Yaw;
		static void gyroSetPosPitch(IMyGyro g, double rpm)
		{
			float before = g.Pitch;
			//g.Pitch = (float)rpm;
			g.SetValueFloat("Pitch", (float)rpm);
			//dbg($"gyroSetPosPitch({g.CustomName}, {rpm:0.00}); before={before:0.00} after={g.Pitch:0.00}");
		}
		static void gyroSetNegPitch(IMyGyro g, double rpm)
		{
			//g.Pitch = (float)(-rpm);
			g.SetValueFloat("Pitch", (float)(-rpm));
			//dbg($"gyroSetNegPitch({g.CustomName}, {rpm:0.00})");
		}
		static void gyroSetPosRoll(IMyGyro g, double rpm)
		{
			//g.Roll = (float)rpm;
			g.SetValueFloat("Roll", (float)rpm);
			//dbg($"gyroSetPosRoll({g.CustomName}, {rpm:0.00})");
		}
		static void gyroSetNegRoll(IMyGyro g, double rpm)
		{
			//g.Roll = (float)(-rpm);
			g.SetValueFloat("Roll", (float)(-rpm));
			//dbg($"gyroSetNegRoll({g.CustomName}, {rpm:0.00})");
		}
		static void gyroSetPosYaw(IMyGyro g, double rpm)
		{
			//g.Yaw = (float)rpm;
			g.SetValueFloat("Yaw", (float)rpm);
			//dbg($"gyroSetPosYaw({g.CustomName}, {rpm:0.00})");
		}
		static void gyroSetNegYaw(IMyGyro g, double rpm)
		{
			//g.Yaw = (float)(-rpm);
			g.SetValueFloat("Yaw", (float)(-rpm));
			//dbg($"gyroSetNegYaw({g.CustomName}, {rpm:0.00})");
		}
		class GyroTranslator
		{
			public IMyGyro g;
			public GyroOverrideGetter getPitch;
			public GyroOverrideGetter getRoll;
			public GyroOverrideSetter setRoll;
			public GyroOverrideSetter setPitch;
			public GyroOverrideGetter getYaw;
			public GyroOverrideSetter setYaw;
			public GyroTranslator(IMyGyro gyro, IMyTerminalBlock cockpit) //, Program p)
			{
				g = gyro;
				var ds = new StringBuilder();
				ds.Append(g.CustomName);

				//switch (g.Orientation.Forward)
				switch (GridDirectionToBlock(cockpit, g.Orientation.Forward))
				{
					case Base6Directions.Direction.Forward:
						ds.Append(" FF");
						getRoll = gyroGetPosRoll;
						setRoll = gyroSetPosRoll;
						break;
					case Base6Directions.Direction.Backward:
						ds.Append(" FB");
						getRoll = gyroGetNegRoll;
						setRoll = gyroSetNegRoll;
						break;
					case Base6Directions.Direction.Right:
						ds.Append(" FR");
						getPitch = gyroGetPosRoll;
						setPitch = gyroSetPosRoll;
						break;
					case Base6Directions.Direction.Left:
						ds.Append(" FL");
						getPitch = gyroGetNegRoll;
						setPitch = gyroSetNegRoll;
						break;
					case Base6Directions.Direction.Up:
						ds.Append(" FU");
						getYaw = gyroGetNegRoll;
						setYaw = gyroSetNegRoll;
						break;
					case Base6Directions.Direction.Down:
						ds.Append(" FD");
						getYaw = gyroGetPosRoll;
						setYaw = gyroSetPosRoll;
						break;
				}
				switch (GridDirectionToBlock(cockpit, g.Orientation.Up))
				{
					case Base6Directions.Direction.Forward:
						ds.Append(" UF");
						//getRoll = gyroGetPosYaw;
						//setRoll = gyroSetPosYaw;
						getRoll = gyroGetNegYaw;
						setRoll = gyroSetNegYaw;
						break;
					case Base6Directions.Direction.Backward:
						ds.Append(" UB");
						//getRoll = gyroGetNegYaw;
						//setRoll = gyroSetNegYaw;
						getRoll = gyroGetPosYaw;
						setRoll = gyroSetPosYaw;
						break;
					case Base6Directions.Direction.Right:
						ds.Append(" UR");
						//getPitch = gyroGetPosYaw;
						//setPitch = gyroSetPosYaw;
						getPitch = gyroGetNegYaw;
						setPitch = gyroSetNegYaw;
						break;
					case Base6Directions.Direction.Left:
						ds.Append(" UL");
						//getPitch = gyroGetNegYaw;
						//setPitch = gyroSetNegYaw;
						getPitch = gyroGetPosYaw;
						setPitch = gyroSetPosYaw;
						break;
					case Base6Directions.Direction.Up:
						ds.Append(" UU");
						getYaw = gyroGetPosYaw;
						setYaw = gyroSetPosYaw;
						break;
					case Base6Directions.Direction.Down:
						ds.Append(" UD");
						getYaw = gyroGetNegYaw;
						setYaw = gyroSetNegYaw;
						break;
				}
				switch (GridDirectionToBlock(cockpit, g.Orientation.Left))
				{
					case Base6Directions.Direction.Left:
						ds.Append(" LL");
						getPitch = gyroGetPosPitch;
						setPitch = gyroSetPosPitch;
						break;
					case Base6Directions.Direction.Right:
						ds.Append(" LR");
						getPitch = gyroGetNegPitch;
						setPitch = gyroSetNegPitch;
						break;
					case Base6Directions.Direction.Forward:
						ds.Append(" LF");
						//getRoll = gyroGetPosPitch;
						//setRoll = gyroSetPosPitch;
						getRoll = gyroGetNegPitch;
						setRoll = gyroSetNegPitch;
						break;
					case Base6Directions.Direction.Backward:
						ds.Append(" LB");
						//getRoll = gyroGetNegPitch;
						//setRoll = gyroSetNegPitch;
						getRoll = gyroGetPosPitch;
						setRoll = gyroSetPosPitch;
						break;
					case Base6Directions.Direction.Up:
						ds.Append(" LU");
						getYaw = gyroGetPosPitch;
						setYaw = gyroSetPosPitch;
						break;
					case Base6Directions.Direction.Down:
						ds.Append(" LD");
						getYaw = gyroGetNegPitch;
						setYaw = gyroSetNegPitch;
						break;
				}
				//ds.Append("---");
				dbg(ds.ToString());
			}
		}
		List<IMyGyro> gyros = new List<IMyGyro>();
		List<GyroTranslator> gyt = new List<GyroTranslator>();

		// This might have been a bad idea, making it an identity const
		const double AutoResponse = 1.0;

		void FindGyros(IMyShipController cockpit)
		{
			gyros.Clear();
			gyt.Clear();
			G.GetBlocksOfType(gyros);
			foreach (var g in gyros)
			{
				if (autopilotOn) { g.GyroOverride = true; }
				else { g.GyroOverride = false; }
				gyt.Add(new GyroTranslator(g, cockpit));
			}
			
			//double torqueToMassRatio = (double)gyros.Count / (double)activeCockpit.CalculateShipMass().TotalMass;
			//if (activeCockpit.CubeGrid.GridSizeEnum == MyCubeSize.Large) { torqueToMassRatio *= 5.0; }
			////autoResponseFactor = torqueToMassRatio / (1.0 / 5000.0);
			//AutoResponse = torqueToMassRatio * 5000.0;

			ReconfigurePIDs();
		}

		void FindBlocks()
		{
			var cockpit = GetCockpit();
			FindThrust();
			FindGyros(cockpit);
			remotes.Clear();
			G.GetBlocksOfType(remotes);
			if (remotes.Count == 1)
			{
				rc = remotes[0];
				currentRemoteControlBlockName = rc.DisplayName;
				waypoints.Clear();
				rc.GetWaypointInfo(waypoints);
			}
		}

		class NavInfo
		{
			public Vector3D position;
			public Vector3D direction;
			public double heading;
			public double pitch;
			public double latitude;
			public double longitude;
			public double seaLevelAltitude;
			public NavInfo()
			{
				position = new Vector3D();
				direction = new Vector3D();
				heading = pitch = latitude = longitude = seaLevelAltitude = 0;
			}
			public NavInfo(Vector3D pos, Vector3D dir, Vector3D planetPosition)
			{
				init(pos, dir, planetPosition);
			}
			public void init(Vector3D pos, Vector3D dir, Vector3D planetPosition)
			{
				position = pos;
				direction = dir;
				var dirnorm = dir; dirnorm.Normalize();
				var shipPlanetPos = pos - planetPosition;
				//var up = shipPlanetPos; up.Normalize();
				var up = pos - planetPosition; up.Normalize();

				latitude = Math.Asin(up.Dot(negY)) * 180.0 / Math.PI;
				if (shipPlanetPos.X == 0.0) { shipPlanetPos.X = 0.0001; }
				longitude = Math.Atan2(shipPlanetPos.Z, shipPlanetPos.X) * 180.0 / Math.PI;
				//seaLevelAltitude = ((pos - planetPosition) - (planetPosition + up * planetSeaLevelRadius)).Length();
				seaLevelAltitude = (pos - planetPosition).Length() - planetSeaLevelRadius;

				//var verticalFactor = up.Dot(dir);
				var verticalFactor = up.Dot(dirnorm);
				var vertComponent = up * verticalFactor;
				//var horizontal = dir - vertComponent;
				var horizontal = dirnorm - vertComponent;

				var east = negY.Cross(shipPlanetPos); east.Normalize();
				var aboveOrBelowEastWestPlane = up.Dot(east.Cross(dirnorm));
				//var atoe = AngleBetweenDeg(horizontal, east); // angle to east
				if (aboveOrBelowEastWestPlane > 0)
				{
					//Disp("above");
					heading = angmod(90 - AngleBetweenDeg(horizontal, east), 360.0);
				}
				else
				{
					//Disp("below");
					heading = angmod(90 + AngleBetweenDeg(horizontal, east), 360.0);
				}

				pitch = Math.Asin(verticalFactor) * 180.0 / Math.PI;

			}
			public void goTowards(Vector3D from, Vector3D to, Vector3D planetPos)
			{
				init(from, to - from, planetPos);
			}
		}

		MyDetectedEntityInfo? target;
		void DesignateTarget()
		{
			if (!enableTargetCam)
			{
				Popup("TARGETING\r\nDISABLED");
				return;
			}
			var cam = G.GetBlockWithName(TargetDesignatorCam) as IMyCameraBlock;
			if (cam == null)
			{
				Popup("NO CAMERA", 1);
				target = null;
				return;
			}
			var avail = cam.AvailableScanRange - 1.0;
			target = cam.Raycast(avail);
			if (target.Value.IsEmpty()) // || !target.Value.HitPosition.HasValue)
			{
				Popup($"NO TARGET\r\n{avail:0} m", 1);
				target = null;
				return;
			}
			else
			{
				FindBlocks();
				UpdateNav();
				Popup("ACQUIRED", 1);
				Runtime.UpdateFrequency = UpdateFrequency.Update1;
				if (!autopilotOn)
				{
					if (thrustController.setpoint < 10) { thrustController.setpoint = Clamp(activeCockpit.GetShipSpeed(), 0, MaxSpeed); }
					if (altitudeController.setpoint < 100) { altitudeController.setpoint = altitude; }
					setHeading = angmod(heading);
				}
				autopilotOn = true;
			}
		}

		DateTime popupExpire = DateTime.Now;
		string popupMsg;
		void Popup(string msg, double duration = 3.0f)
		{
			popupMsg = msg;
			popupExpire = DateTime.Now + TimeSpan.FromSeconds(duration);
			Echo(msg);
		}

		void ResumeRoute()
		{
			FindBlocks();
			if (rc == null)
			{
				Popup("NO REMOTE");
				return;
			}
			
			waypoints.Clear();
			rc.GetWaypointInfo(waypoints);
			if (waypoints.Count == 0)
			{
				Popup("NO WAYPOINTS");
				waypointMode = false;
			}
			nextWaypointIndex = (int)Clamp(nextWaypointIndex, 0, waypoints.Count);
		}


		bool displayIsInitialized = false;
		string[] panelText = new string[10];
		StringBuilder[] panelTextLines = new StringBuilder[10];
		//string[] newlineSplitSeparator = { newline };
		char[] newlineSplitSeparator = { '\r', '\n' };
		void RunDisplays()
		{
			if (!enableDisplay) { return; }
			if (!displayIsInitialized)
			{
				//foreach (var sb in panelTextLines) { sb = new StringBuilder(); }
				for (int i =0; i < panelTextLines.Length; i++)
				{
					panelTextLines[i] = new StringBuilder();
				}
				displayIsInitialized = true;
			}
			foreach (var d in Displays)
			{
				//dbg("RunDisplays() 1");
				var b = G.GetBlockWithName(d); // d.block);
				if (b == null)
				{
					//Echo($"Display not found: {d.block}");
					Echo($"Display not found: {d}");
					continue;
				}

				//dbg("RunDisplays() 2");
				if (b.CustomData.Length > 0)
				{
					//dbg("RunDisplays() 3");
					foreach (var pb in panelTextLines) { pb.Clear(); }
					var lines = b.CustomData.Split(newlineSplitSeparator, StringSplitOptions.RemoveEmptyEntries);
					int iPanel = 0;
					foreach (var nextline in lines)
					{
						var line = nextline.Trim();
						//dbg("RunDisplays() 4");
						if (line.StartsWith("panel"))
						{
							//dbg(line.Substring(5)); // TODO: Remove
							iPanel = int.Parse(line.Substring(5).Trim());
							if (iPanel >= panelText.Length) { continue; }
						}
						else
						{
							panelTextLines[iPanel].Append(line).Append(newline);
						}
						//dbg("RunDisplays() 5");
					}
					//dbg("RunDisplays() 6");
					for (iPanel = 0; iPanel < panelText.Length; iPanel++)
					{
						//dbg("RunDisplays() 7");
						panelText[iPanel] = panelTextLines[iPanel].ToString();
					}
				}

				//dbg("RunDisplays() 8");
				IMyTextSurface surf = null;
				if (b is IMyTextSurfaceProvider)
				{
					var p = b as IMyTextSurfaceProvider;

					//dbg("RunDisplays() 9");
					for (int iSurface = 0; iSurface < p.SurfaceCount; iSurface++)
					{
						//dbg($"RunDisplays() 10 iSurface = {iSurface}");
						if (panelText[iSurface] != null && panelText[iSurface].Length > 0)
						{
							//dbg("RunDisplays() 11");
							surf = p.GetSurface(iSurface);
							RunDisplaySurface(surf, panelText[iSurface]);
						}
					}
					//if (d.panel < 0 || d.panel >= p.SurfaceCount)
					//{
					//	Echo($"Display '{d.block}' does not have {d.panel} panels.");
					//	continue;
					//}
					//surf = p.GetSurface(d.panel);
				}
				else if (b is IMyTextSurface)
				{
					//dbg("RunDisplays() 12");
					surf = b as IMyTextSurface;
					RunDisplaySurface(surf, panelText[0]);
				}

				//if (surf == null)
				//{
				//	dbg("RunDisplays() 13");
				//	Echo($"Could not get text surface for display '{d.block}'");
				//	return;
				//}
				//dbg("RunDisplays() 14");
			}
			//dbg("RunDisplays() 15");
			DebugText.Clear();
		}

		void RunDisplaySurface(IMyTextSurface surf, string s)
		{
			//var s = new StringBuilder(d.text);
			//string s = d.defaultText;
			if (s == null) s = "";

			surf.ContentType = ContentType.TEXT_AND_IMAGE;
			if (Runtime.UpdateFrequency == UpdateFrequency.None)
			{
				surf.WriteText("NAV OFF");
			}
			else if (DateTime.Now < popupExpire)
			{
				Echo(popupMsg);
				surf.WriteText(popupMsg);
			}
			else
			{
				//!display substitution
				s = sr(s, "[status]", status);
				s = sr(s, "[mode]", navmode);
				s = sr(s, "[loopmode]", () => loopmode());
				s = sr(s, "[loopdistance]", () => loopdistance());
				s = sr(s, "[altmode]", () => AltitudeMode.ToString());
				s = sr(s, "[alt]", () => $"{altitude:0}");
				s = sr(s, "[altgoal]", () => $"{altitudeController.setpoint:0}");
				s = sr(s, "[hdg]", () => $"{heading:0.0}");
				s = sr(s, "[hdggoal]", () => $"{setHeading:0.0}");
				s = sr(s, "[spd]", () => $"{activeCockpit.GetShipSpeed():0}");
				s = sr(s, "[spdgoal]", () => $"{thrustController.setpoint:0}");
				s = sr(s, "[gps]", llstr);
				s = sr(s, "[lat]", () => $"{lat:0.00}");
				s = sr(s, "[lon]", () => $"{lon:0.00}");
				//s = sr(ref s, "[dtt]", () => target.HasValue ? (target.Value.HitPosition.Value - shipWorldPos).Length().ToString() : "--");
				s = sr(s, "[dtt]", () => str(dtt(), "0", "--"));
				s = sr(s, "[hdtt]", () => str(hdtt(), "0", "--"));
				//s = sr(s, "[dtw]", () => str(dtw(), "0", "--"));
				//s = sr(s, "[hdtw]", () => str(hdtw(), "0", "--"));
				s = sr(s, "[target]", () => target?.Name ?? GetWaypoint()?.Name ?? "--");
				s = sr(s, "[cpu]", () => $"{cpu}");
				s = sr(s, "[hot]", () => $"{hot():0}");
				//s = sr(s, "[waypoint]", () => GetWaypoint()?.Name ?? "--");
				s = sr(s, "[ttt]", () => tttstr());
				s = sr(s, "[bombtime]", () => bombtimestr());
				s = sr(s, "[roll]", () => $"{roll:0.0}");
				s = sr(s, "[rollgoal]", () => $"{rollController.setpoint:0.0}");
				s = sr(s, "[pitch]", () => $"{pitch:0.0}");
				s = sr(s, "[pitchgoal]", () => $"{pitchController.setpoint:0.0}");
				s = sr(s, "[sideslip]", () => $"{sideslip:0.0}");
				s = sr(s, "[thrust]", () => $"{thrustController.Output * 100:0}");
				s = sr(s, "[debug]", () => $"{DebugText}");
				s = sr(s, "[radius]", () => $"{circleRadius:0}");
				s = sr(s, "[bombcount]", () => $"{bombcount()}");
				s = sr(s, "[localizer]", () => currentLocalizerName);
				s = sr(s, "[pitchresponse]", () => $"{PitchResponse:0.00}");
				s = sr(s, "[rollresponse]", () => $"{RollResponse:0.00}");
				s = sr(s, "[autoresponse]", () => $"{AutoResponse:0.00}");
				s = sr(s, "[aoa]", () => $"{aoa(activeCockpit.GetShipVelocities().LinearVelocity, shipWorld):0.0}");
				//s = sr(s, "[]", () => $"{}");
				
				// Template
				//s = sr(s, "[]", () => $"{}");
				dbg(s);
				surf.WriteText(s);
			}

		}

		public delegate T DeferredEvaluator<T>();
		string sr(string src, string scan, DeferredEvaluator<string> rep)
		{
			if (src.Contains(scan))
			{
				return src.Replace(scan, rep());
			}
			return src;
		}
		string sr(string src, string scan, string rep) { return src.Replace(scan, rep); }

		string str(double? x, string format = "", string ifnull = "")
		{
			if (x.HasValue) return x.Value.ToString(format);
			else return ifnull;
		}

		double? dtt() // distance to target
		{
			if (target.HasValue)
			{
				return (target.Value.HitPosition.Value - shipWorldPos).Length();
			}
			return dtw();
		}
		double? hdtt()
		{
			if (target.HasValue)
			{
				return horiz(shipWorldPos, target.Value.HitPosition.Value).Length();
			}
			return hdtw();
		}
		double? dtw()
		{
			// Distance to waypoint
			try
			{
				if (waypointMode)
				{
					return (waypoints[nextWaypointIndex].Coords - shipWorldPos).Length();
				}
			}
			catch { }
			return null;
		}
		double? hdtw()
		{
			// horizontal distance to waypoint
			try
			{
				if (waypointMode)
				{
					return horiz(shipWorldPos, waypoints[nextWaypointIndex].Coords).Length();
				}
			}
			catch { }
			return null;
		}

		Vector3D horiz(Vector3D from, Vector3D to)
		{
			// Horizontal distance
			var up = from - planetWorldPos;
			up.Normalize();
			var diff = to - from;
			var vert = up.Dot(diff);
			//Echo($"vert {vert}"); // TODO: remove
			//Echo($"horiz {(diff - (vert * up)).Length()}");
			return (diff - (vert * up));
		}

		MyWaypointInfo? GetWaypoint()
		{
			if (!waypointMode) return null;
			if (nextWaypointIndex < 0 || nextWaypointIndex >= waypoints.Count) return null;
			return waypoints[nextWaypointIndex];
		}

		Vector3D? GetTargetCoords()
		{
			if (target.HasValue) { return target.Value.HitPosition.Value; }
			if (waypointMode && nextWaypointIndex >= 0 && nextWaypointIndex < waypoints.Count) { return waypoints[nextWaypointIndex].Coords; }
			return null;
		}

		double hot()
		{
			// height above target
			var t = GetTargetCoords();
			if (!t.HasValue) { return 0; }
			return (shipPlanetPos).Length() - (t.Value - planetWorldPos).Length();
		}


		double ttt()
		{
			// time to target
			var d = hdtt();
			if (!d.HasValue) { return 0; }
			return d.Value / activeCockpit.GetShipSpeed();
		}
		string tttstr()
		{
			var t = TimeSpan.FromSeconds(ttt());
			return $"{t.Minutes:0}:{t.Seconds:D2}";
		}

		double falltime(double height)
		{
			var g = activeCockpit.GetNaturalGravity().Length();
			var tterm = HardSpeedLimit / g;
			var yterm = .5 * g * tterm * tterm;
			if (height < yterm) { return Math.Sqrt(height / (.5 * g)); }
			else { return (height - yterm) / HardSpeedLimit; }
		}

		string bombtimestr()
		{
			try
			{
				var t = TimeSpan.FromSeconds(ttt() - falltime(hot()));
				return $"{t.Minutes:0}:{t.Seconds:D2}.{t.Milliseconds / 100}";
			}
			catch { return "-:--"; }
		}

		void DropNextBomb()
		{
			try
			{
				if (bombDropIndex >= BombPylons.Length)
				{
					bombDropSequenceRunning = false;
					//bombDropIndex = 0;
					return;
				}
				var b = G.GetBlockWithName(BombPylons[bombDropIndex]);
				b.ApplyAction(BombDropAction);
				bombDropIndex++;
				lastBombDropTime = DateTime.Now;
			}
			catch (Exception e)
			{
				Popup("BOMB ERROR" + newline + e.Message, 5);
			}
		}

		string loopmode()
		{
			if (circleMode) { return "CIRCLE"; }
			return enableLoopBackToTarget ? "LOOP" : "ONCE";
		}

		string loopdistance()
		{
			if (circleMode) return circleRadius.ToString("0");
			return TargetLoopBackDistance.ToString("0");
		}

		int bombcount()
		{
			return Math.Max(0, BombPylons.Length - bombDropIndex);
		}

		const Base6Directions.Direction _up = Base6Directions.Direction.Up;
		const Base6Directions.Direction _down = Base6Directions.Direction.Down;
		const Base6Directions.Direction _left = Base6Directions.Direction.Left;
		const Base6Directions.Direction _right = Base6Directions.Direction.Right;
		const Base6Directions.Direction _forward = Base6Directions.Direction.Forward;
		const Base6Directions.Direction _backward = Base6Directions.Direction.Backward;

		static Base6Directions.Direction OppositeDirection(Base6Directions.Direction d)
		{
			switch (d)
			{
				case _up: return _down;
				case _down: return _up;
				case _left: return _right;
				case _right: return _left;
				case _forward: return _backward;
				case _backward: return _forward;
				default: throw new Exception("Unknown Base6Direction");
			}
		}
		static Base6Directions.Direction GridDirectionToBlock(IMyTerminalBlock block, Base6Directions.Direction d)
		{
			if (block == null)
			{
				dbg("Warning, cockpit is null: " + new Exception().StackTrace);
				return d;
			}
			//switch (block.Orientation.Up)
			if (d == block.Orientation.Up) return _up;
			if (d == OppositeDirection(block.Orientation.Up)) return _down;
			if (d == block.Orientation.Left) return _left;
			if (d == OppositeDirection(block.Orientation.Left)) return _right;
			if (d == block.Orientation.Forward) return _forward;
			if (d == OppositeDirection(block.Orientation.Forward)) return _backward;
			throw new Exception("Unknown Base6Direction");
		}

		enum CloseOrFar
		{
			CLOSE,
			FAR
		}

		NavInfo getApproachAlt = new NavInfo();
		double landingSlopeToLineup = 0;

		void RecalcLanding(NavInfo shipNav, Vector3D landingTargetPos, Vector3D lineupHorizontal, CloseOrFar lineupRelation)
		{
			landingTarget = landingTargetPos;
			var lineupNorm = horiz(landingTargetPos, lineupHorizontal); lineupNorm.Normalize();
			var up = landingTargetPos - planetWorldPos; up.Normalize();
			if (lineupRelation == CloseOrFar.FAR) { lineupNorm *= -1; }
			// 10 degree slope
			beginFinalApproachPoint = landingTarget
				+ (LandingFinalApproachDistance * lineupNorm)
				+ (LandingFinalApproachDistance * Math.Sin(LandingGlideSlope * Math.PI / 180.0) * up);
			getApproachAlt.goTowards(beginFinalApproachPoint, landingTarget, planetWorldPos);
			if (ilsMode && !ilsFinal)
			{
				AltitudeMode = MyPlanetElevation.Sealevel;
				altitudeController.setpoint = getApproachAlt.seaLevelAltitude
					+ horiz(beginFinalApproachPoint, shipNav.position).Length() * Math.Sin(landingSlopeToLineup);
			}
		}
		void Land(NavInfo shipNav, Vector3D landingTargetPos, Vector3D lineupHorizontal, CloseOrFar lineupRelation)
		{
			RecalcLanding(shipNav, landingTargetPos, lineupHorizontal, lineupRelation);
			getApproachAlt.goTowards(beginFinalApproachPoint, landingTarget, planetWorldPos);
			landingSlopeToLineup = Math.Atan2(
				shipNav.seaLevelAltitude - getApproachAlt.seaLevelAltitude,
				horiz(shipNav.position, beginFinalApproachPoint).Length());
			//altitudeController.setpoint = ((beginFinalApproachPoint - planetWorldPos) - (planetWorldPos + up * planetSeaLevelRadius)).Length();
			//altitudeController.setpoint = getApproachAlt.seaLevelAltitude;
			AltitudeMode = MyPlanetElevation.Sealevel;
			thrustController.setpoint = LandingLineupToRunwaySpeed;
			ilsMode = true;
			ilsFinal = false;
			navmode = "LAND";
		}

		// Angle of attack
		double aoa(Vector3D velocity, MatrixD shipMat)
		{
			var right = shipMat.Right;
			var velInPitchPlane = velocity - (right.Dot(velocity) * right);
			return AngleBetweenDeg(shipMat.Forward, velInPitchPlane);
		}





		void dfp_test()
		{
			FindBlocks();
			//UpdateNav();
			//Vector3D pp;
			//activeCockpit.TryGetPlanetPosition(out pp);
			//Echo("planet at " + pp.ToString());
			//Echo($"shipPlanetPos {shipPlanetPos}");
			//Echo($"alt {navMain.seaLevelAltitude}");
			//Echo($"pitch {navMain.pitch}");
			
			foreach (var g in gyt)
			{
				g.g.GyroOverride = false;
				g.setRoll(g.g, 0);
				g.setPitch(g.g, 0);
				g.setYaw(g.g, 0);

				Echo($"{g.g.GyroPower}");
			}
		}

		// END_CLIP

	} // Program

}
