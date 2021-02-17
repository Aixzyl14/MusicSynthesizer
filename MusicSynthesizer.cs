#define FILE_MENU_NEW 1
#define FILE_MENU_OPEN 2
#define FILE_MENU_EXIT 3
#define FILE_MENU_HELP 4
#define CHANGE_TITLE 5
#define ProduceMusic 6
#define SlowBeat 7
#define FastBeat 8
#define MediumBeat 9
#define MelodicPiano 10
#define Harmonica 11
#define RetroSound 12
#define FTYPE double

# include <list>
# include <iostream>
# include <algorithm>
# include<Windows.h>
# include"MMSystem.h"
using namespace std;

LRESULT CALLBACK WindowProcedure(HWND, UINT, WPARAM, LPARAM);

void AddMenus(HWND);
void AddHelpMenus(HWND);
void AddControls(HWND);
void AddControlsHelp(HWND);
void LoadImages();
void LoadImagesHelp();
void RegisterHelpClass(HINSTANCE);
void DisplayHelpClass(HWND);
//global
HWND hbitmapdisplay;
HWND hbitmapdisplayHelp;
HBITMAP hmybitmap;
HBITMAP hmybitmaphelp;
HMENU hMenu;
HWND hEdit;
bool slow = false;
bool medium = false;
bool fast = false;
bool MPiano = false;
bool Harmonicaa = false;
bool Retro = false;
bool Exit = false;
bool Exit2 = false;
int nBeats;
int nSubBeats;
int nCurrentBeat;
int nTotalBeats;
FTYPE fTempo;
FTYPE fBeatTime;
FTYPE fAccumulate;

# include "olcNoiseMaker.h"


namespace synth
{
	//////////////////////////////////////////////////////////////////////////////
	// Utilities

	// Converts frequency (Hz) to angular velocity (dHertz = frequency)
	FTYPE w(const FTYPE dHertz)
	{
		return dHertz * 2.0 * PI;
	}

	struct instrument_base;

	// A basic note
	struct note
	{
		int id;     // Position in scale
		FTYPE on;   // Time note was activated
		FTYPE off;  // Time note was deactivated
		bool active;
		instrument_base* channel;

		note()
		{
			id = 0;
			on = 0.0;
			off = 0.0;
			active = false;
			channel = nullptr;
		}

		//bool operator==(const note& n1, const note& n2) { return n1.id == n2.id; }
	};

	//////////////////////////////////////////////////////////////////////////////
	// Multi-Function Oscillator
	const int OSC_SINE = 0;
	const int OSC_SQUARE = 1;
	const int OSC_TRIANGLE = 2;
	const int OSC_SAW_ANA = 3;
	const int OSC_SAW_DIG = 4;
	const int OSC_NOISE = 5;

	FTYPE osc(const FTYPE dTime, const FTYPE dHertz, const int nType = OSC_SINE,
		const FTYPE dLFOHertz = 0.0, const FTYPE dLFOAmplitude = 0.0, FTYPE dCustom = 50.0)
	{

		FTYPE dFreq = w(dHertz) * dTime + dLFOAmplitude * dHertz * (sin(w(dLFOHertz) * dTime)); // LFO = Low Frequency Oscillator

		switch (nType)
		{
			case OSC_SINE: // Sine wave bewteen -1 and +1
				return sin(dFreq);

			case OSC_SQUARE: // Square wave between -1 and +1
				return sin(dFreq) > 0 ? 1.0 : -1.0;

			case OSC_TRIANGLE: // Triangle wave between -1 and +1
				return asin(sin(dFreq)) * (2.0 / PI);

			case OSC_SAW_ANA: // Saw wave (analogue / warm / slow)
				{
					FTYPE dOutput = 0.0;
					for (FTYPE n = 1.0; n < dCustom; n++)
						dOutput += (sin(n * dFreq)) / n;
					return dOutput * (2.0 / PI);
				}

			case OSC_SAW_DIG: // Saw Wave (Digital)
				return (2.0 / PI) * (dHertz * PI * fmod(dTime, 1.0 / dHertz) - (PI / 2.0));

			case OSC_NOISE:
				return 2.0 * ((FTYPE)rand() / (FTYPE)RAND_MAX) - 1.0;

			default:
				return 0.0;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Scale to Frequency conversion

	const int SCALE_DEFAULT = 0;

	FTYPE scale(const int nNoteID, const int nScaleID = SCALE_DEFAULT)
	{
		switch (nScaleID)
		{
			case SCALE_DEFAULT:
			default:
				return 8 * pow(1.0594630943592952645618252949463, nNoteID);
		}
	}


	//////////////////////////////////////////////////////////////////////////////
	// Envelopes

	struct envelope
	{
		virtual FTYPE amplitude(const FTYPE dTime, const FTYPE dTimeOn, const FTYPE dTimeOff) = 0;
	};

	struct envelope_adsr : public envelope
	{
		FTYPE dAttackTime;
	FTYPE dDecayTime;
	FTYPE dSustainAmplitude;
	FTYPE dReleaseTime;
	FTYPE dStartAmplitude;

	envelope_adsr()
	{
		dAttackTime = 0.1;
		dDecayTime = 0.1;
		dSustainAmplitude = 1.0;
		dReleaseTime = 0.2;
		dStartAmplitude = 1.0;
	}

	virtual FTYPE amplitude(const FTYPE dTime, const FTYPE dTimeOn, const FTYPE dTimeOff)
	{
		FTYPE dAmplitude = 0.0;
		FTYPE dReleaseAmplitude = 0.0;

		if (dTimeOn > dTimeOff) // Note is on
		{
			FTYPE dLifeTime = dTime - dTimeOn;

			if (dLifeTime <= dAttackTime)
				dAmplitude = (dLifeTime / dAttackTime) * dStartAmplitude;

			if (dLifeTime > dAttackTime && dLifeTime <= (dAttackTime + dDecayTime))
				dAmplitude = ((dLifeTime - dAttackTime) / dDecayTime) * (dSustainAmplitude - dStartAmplitude) + dStartAmplitude;

			if (dLifeTime > (dAttackTime + dDecayTime))
				dAmplitude = dSustainAmplitude;
		}
		else // Note is off
		{
			FTYPE dLifeTime = dTimeOff - dTimeOn;

			if (dLifeTime <= dAttackTime)
				dReleaseAmplitude = (dLifeTime / dAttackTime) * dStartAmplitude;

			if (dLifeTime > dAttackTime && dLifeTime <= (dAttackTime + dDecayTime))
				dReleaseAmplitude = ((dLifeTime - dAttackTime) / dDecayTime) * (dSustainAmplitude - dStartAmplitude) + dStartAmplitude;

			if (dLifeTime > (dAttackTime + dDecayTime))
				dReleaseAmplitude = dSustainAmplitude;

			dAmplitude = ((dTime - dTimeOff) / dReleaseTime) * (0.0 - dReleaseAmplitude) + dReleaseAmplitude;
		}

		// Amplitude should not be negative
		if (dAmplitude <= 0.01)
			dAmplitude = 0.0;

		return dAmplitude;
	}
};

FTYPE env(const FTYPE dTime, envelope &env, const FTYPE dTimeOn, const FTYPE dTimeOff)
{
	return env.amplitude(dTime, dTimeOn, dTimeOff);
}


struct instrument_base
{
	FTYPE dVolume;
	synth::envelope_adsr env;
	FTYPE fMaxLifeTime;
	wstring name;
	virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished) = 0;
	};

struct instrument_bell : public instrument_base
	{
		instrument_bell()
{
	env.dAttackTime = 0.01;
	env.dDecayTime = 1.0;
	env.dSustainAmplitude = 0.0;
	env.dReleaseTime = 1.0;
	fMaxLifeTime = 3.0;
	dVolume = 1.0;
	name = L"Bell";
}

virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
{
	FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
	if (dAmplitude <= 0.0) bNoteFinished = true;

	FTYPE dSound =
		+1.00 * synth::osc(dTime - n.on, synth::scale(n.id + 12), synth::OSC_SINE, 5.0, 0.001)
		+ 0.50 * synth::osc(dTime - n.on, synth::scale(n.id + 24))
		+ 0.25 * synth::osc(dTime - n.on, synth::scale(n.id + 36));

	return dAmplitude * dSound * dVolume;
}

};

struct instrument_bell8 : public instrument_base
{
	instrument_bell8()
		{
		env.dAttackTime = 0.01;
		env.dDecayTime = 0.5;
		env.dSustainAmplitude = 0.8;
		env.dReleaseTime = 1.0;
		fMaxLifeTime = 3.0;
		dVolume = 1.0;
		name = L"8-Bit Bell";
	}

		virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
		{
		FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
		if (dAmplitude <= 0.0) bNoteFinished = true;

		FTYPE dSound =
			+1.00 * synth::osc(dTime - n.on, synth::scale(n.id), synth::OSC_SQUARE, 5.0, 0.001)
			+ 0.50 * synth::osc(dTime - n.on, synth::scale(n.id + 12))
			+ 0.25 * synth::osc(dTime - n.on, synth::scale(n.id + 24));

		return dAmplitude * dSound * dVolume;
	}

};

struct instrument_Piano : public instrument_base
{
	HWND hWnd;
	instrument_Piano()
		{
		env.dAttackTime = 0.00;
		env.dDecayTime = 1.0;
		env.dSustainAmplitude = 0.14;
		env.dReleaseTime = 0.1;
		fMaxLifeTime = 1.0;
		name = L"Piano";
		dVolume = 1.0;
	}
		virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
		{
		int answer;
		FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
		if (dAmplitude <= 0.0) bNoteFinished = true;
		if (MPiano == true)
		{


			FTYPE dSound =     // Melodic Piano
				+(1.0 * 2) * synth::osc(n.on - dTime, synth::scale(n.id - 12), synth::OSC_SINE, 2.0, 0.005, 100)
				+ (1.00 * 3) * synth::osc(dTime - n.on, synth::scale(n.id), synth::OSC_SINE, 1.0, 0.005)
				+ (0.50 * 4) * synth::osc(dTime - n.on, synth::scale(n.id + 12), synth::OSC_SINE);

			return dAmplitude * dSound * dVolume;

		}
		else
		{

		}
	}
};
struct instrument_Harmonica : public instrument_base
{
	HWND hWnd;

	instrument_Harmonica()
		{
		env.dAttackTime = 0.00;
		env.dDecayTime = 1.0;
		env.dSustainAmplitude = 0.95;
		env.dReleaseTime = 0.1;
		fMaxLifeTime = -1.0;
		name = L"Harmonica";
		dVolume = 1.0;
	}

		

		virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
		{
		int answer;
		FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
		if (dAmplitude <= 0.0) bNoteFinished = true;
		if (Harmonicaa)
		{

			FTYPE dSound =     // New Sound
		+1.0 * synth::osc(dTime - n.on, synth::scale(n.id), synth::OSC_TRIANGLE, 1.0, 0.01)
		+ (0.50 * 4) * synth::osc(dTime - n.on, synth::scale(n.id + 12), synth::OSC_SQUARE, 3.0);
			+(0.5 * 2) * synth::osc(dTime - n.on, synth::scale(n.id + 24), synth::OSC_NOISE);


			return dAmplitude * dSound * dVolume;
		}
		else if (MPiano == true)
		{


			FTYPE dSound =     // Melodic Piano
				+(1.0 * 2) * synth::osc(n.on - dTime, synth::scale(n.id - 12), synth::OSC_SINE, 2.0, 0.005, 100)
				+ (1.00 * 3) * synth::osc(dTime - n.on, synth::scale(n.id), synth::OSC_SINE, 1.0, 0.005)
				+ (0.50 * 4) * synth::osc(dTime - n.on, synth::scale(n.id + 12), synth::OSC_SINE);

			return dAmplitude * dSound * dVolume;

		}

		else if (Retro == true)
		{
			FTYPE dSound =     // Retro Sound
				+1.0 * synth::osc(n.on - dTime, synth::scale(n.id - 12), synth::OSC_SAW_ANA, 5.0, 0.001, 100)
				+ 1.00 * synth::osc(dTime - n.on, synth::scale(n.id), synth::OSC_SQUARE, 5.0, 0.001)
				+ 0.50 * synth::osc(dTime - n.on, synth::scale(n.id + 12), synth::OSC_SQUARE)
				+ 0.05 * synth::osc(dTime - n.on, synth::scale(n.id + 24), synth::OSC_NOISE);
			return dAmplitude * dSound * dVolume;
		}
		else
		{

			answer = MessageBoxW(NULL, L"You Have Not Selected An Instrument Type", L"Wait!", MB_ABORTRETRYIGNORE | MB_ICONEXCLAMATION);
			switch (answer)
			{
				case IDABORT:
					return DestroyWindow(hWnd);
					break;
				case IDRETRY:
					return 0;
					break;
				case IDIGNORE:
					break;
			}


		}



	}

};


struct instrument_drumkick : public instrument_base
{
	instrument_drumkick()
		{
		env.dAttackTime = 0.01;
		env.dDecayTime = 0.15;
		env.dSustainAmplitude = 0.0;
		env.dReleaseTime = 0.0;
		fMaxLifeTime = 1.5;
		name = L"Drum Kick";
		dVolume = 1.0;
	}

		virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
		{
		FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
		if (fMaxLifeTime > 0.0 && dTime - n.on >= fMaxLifeTime) bNoteFinished = true;

		FTYPE dSound =
			+0.99 * synth::osc(dTime - n.on, synth::scale(n.id - 36), synth::OSC_SINE, 1.0, 1.0)
			+ 0.01 * synth::osc(dTime - n.on, 0, synth::OSC_NOISE);

		return dAmplitude * dSound * dVolume;
	}

};

struct instrument_drumsnare : public instrument_base
{
	instrument_drumsnare()
		{
		env.dAttackTime = 0.0;
		env.dDecayTime = 0.2;
		env.dSustainAmplitude = 0.0;
		env.dReleaseTime = 0.0;
		fMaxLifeTime = 1.0;
		name = L"Drum Snare";
		dVolume = 1.0;
	}

		virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
		{
		FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
		if (fMaxLifeTime > 0.0 && dTime - n.on >= fMaxLifeTime) bNoteFinished = true;

		FTYPE dSound =
			+0.5 * synth::osc(dTime - n.on, synth::scale(n.id - 24), synth::OSC_SINE, 0.5, 1.0)
			+ 0.5 * synth::osc(dTime - n.on, 0, synth::OSC_NOISE);

		return dAmplitude * dSound * dVolume;
	}

};


struct instrument_drumhihat : public instrument_base
{
	instrument_drumhihat()
		{
		env.dAttackTime = 0.01;
		env.dDecayTime = 0.05;
		env.dSustainAmplitude = 0.0;
		env.dReleaseTime = 0.0;
		fMaxLifeTime = 1.0;
		name = L"Drum HiHat";
		dVolume = 0.5;
	}

		virtual FTYPE sound(const FTYPE dTime, synth::note n, bool &bNoteFinished)
		{
		FTYPE dAmplitude = synth::env(dTime, env, n.on, n.off);
		if (fMaxLifeTime > 0.0 && dTime - n.on >= fMaxLifeTime) bNoteFinished = true;

		FTYPE dSound =
			+0.1 * synth::osc(dTime - n.on, synth::scale(n.id - 12), synth::OSC_SQUARE, 1.5, 1)
			+ 0.9 * synth::osc(dTime - n.on, 0, synth::OSC_NOISE);

		return dAmplitude * dSound * dVolume;
	}

};


struct sequencer
{
	public:
		struct channel
	{
		instrument_base* instrument;
		wstring sBeat;
	};

	public:
		sequencer(float tempo = 120.0f, int beats = 4, int subbeats = 4)
	{
		nBeats = beats;
		nSubBeats = subbeats;
		fTempo = tempo;
		fBeatTime = (60.0f / fTempo) / (float)nSubBeats;
		nCurrentBeat = 0;
		nTotalBeats = nSubBeats * nBeats;
		fAccumulate = 0;
	}


	int Update(FTYPE fElapsedTime)
	{
		vecNotes.clear();

		fAccumulate += fElapsedTime;
		while (fAccumulate >= fBeatTime)
		{
			fAccumulate -= fBeatTime;
			nCurrentBeat++;

			if (nCurrentBeat >= nTotalBeats)
				nCurrentBeat = 0;

			int c = 0;
			for (auto v : vecChannel)
			{
				if (v.sBeat[nCurrentBeat] == L'X')
					{
				note n;
				n.channel = vecChannel[c].instrument;
				n.active = true;
				n.id = 64;
				vecNotes.push_back(n);
			}
			c++;
		}
	}



			return vecNotes.size();
		}

void AddInstrument(instrument_base* inst)
{
	channel c;
	c.instrument = inst;
	vecChannel.push_back(c);
}



public:
		vector<channel> vecChannel;
vector<note> vecNotes;


private:

	};

}

vector<synth::note> vecNotes;
mutex muxNotes;
synth::instrument_bell instBell;
synth::instrument_Harmonica instHarmonica;
synth::instrument_drumkick instKick;
synth::instrument_drumsnare instSnare;
synth::instrument_drumhihat instHiHat;
synth::instrument_Piano instPiano;

typedef bool(*lambda)(synth::note const& item);
template <class T>
 void safe_remove(T & v, lambda f)
{
	auto n = v.begin();
	while (n != v.end())
		if (!f(*n))
			n = v.erase(n);
		else
			++n;
}

// Function used by olcNoiseMaker to generate sound waves
// Returns amplitude (-1.0 to +1.0) as a function of time
FTYPE MakeNoise(FTYPE dTime)
{
	unique_lock<mutex> lm(muxNotes);
	FTYPE dMixedOutput = 0.0;

	// Iterate through all active notes, and mix together
	for (auto & n : vecNotes)
	{
	bool bNoteFinished = false;
	FTYPE dSound = 0;

	// Get sample for this note by using the correct instrument and envelope
	if (n.channel != nullptr)
		dSound = n.channel->sound(dTime, n, bNoteFinished);

	// Mix into output
	dMixedOutput += dSound;

	if (bNoteFinished) // Flag note to be removed
		n.active = false;
}
// Plays the sound to your sound card and controls the volume of the sound
safe_remove<vector<synth::note>>(vecNotes, [](synth::note const& item) { return item.active; });
return dMixedOutput * 0.4;
}



int CALLBACK WinMain(HINSTANCE hInst, HINSTANCE hPrevInst, LPSTR args, int ncmdshow)
{

	WNDCLASSW wc = { 0 };

	wc.hbrBackground = (HBRUSH)(COLOR_MENU);
	wc.hCursor = LoadCursor(NULL, IDC_ARROW);
	wc.hInstance = hInst;
	wc.lpszClassName = L"myWindowClass";
	wc.lpfnWndProc = WindowProcedure;

	if (!RegisterClassW(&wc))
		return -1;

	RegisterHelpClass(hInst);

	CreateWindowW(L"myWindowClass", L"Music Synthesizer", WS_OVERLAPPEDWINDOW | WS_VISIBLE, 100, 100, 500, 500, NULL, NULL, NULL, NULL);

	MSG msg = { 0 };

	while (GetMessage(&msg, NULL, NULL, NULL))
	{
		TranslateMessage(&msg);
		DispatchMessage(&msg);

	}


	return 0;

}

LRESULT CALLBACK WindowProcedure(HWND hWnd, UINT msg, WPARAM wp, LPARAM lp)
{

	int answer;
	switch (msg)
	{
		case WM_COMMAND:
			switch (wp)
			{
				case FILE_MENU_EXIT:
					answer = MessageBoxW(hWnd, L"Are You Sure?", L"Wait!", MB_YESNO | MB_ICONEXCLAMATION);
					if (answer == IDYES)
					{
						DestroyWindow(hWnd);
					}
					else if (answer == IDNO)
					{
						//Do Nothing
					}
					break;

					break;
				case FILE_MENU_HELP:
					DisplayHelpClass(hWnd);
					break;
					break;
				case CHANGE_TITLE:
					wchar_t text[100];
					GetWindowTextW(hEdit, text, 100);
					SetWindowTextW(hWnd, text);
					break;

				case SlowBeat:

					{
						slow = true;
						medium = false;
						fast = false;
					}

					break;
				case MediumBeat:


					{

						medium = true;
						fast = false;
						slow = false;
					}
					break;
				case FastBeat:
					{
						fast = true;
						medium = false;
						slow = false;
					}
					break;
				case MelodicPiano:
					{
						MPiano = true;
						Harmonicaa = false;
						Retro = false;
					}
					break;
				case Harmonica:
					{
						Harmonicaa = true;
						MPiano = false;
						Retro = false;
					}
					break;
				case RetroSound:
					{
						Retro = true;
						Harmonicaa = false;
						MPiano = false;
					}
					break;
				case ProduceMusic:
					{

						synth::sequencer seq(90.0);
						if (Exit2 == true)
						{

							return 0;
						}
						else
							seq.AddInstrument(&instKick);
						seq.AddInstrument(&instSnare);
						seq.AddInstrument(&instHiHat);

						if (fast == true)
						{
							seq.vecChannel.at(0).sBeat = L"XX..X.X.X..X.X..";
							seq.vecChannel.at(1).sBeat = L"X.X.X.X.X.X...X.";
							seq.vecChannel.at(2).sBeat = L"XX.XX.X.X.X.X.XX";
						}
						else if (medium == true)
						{
							seq.vecChannel.at(0).sBeat = L"X.X..X...X..X..X";
							seq.vecChannel.at(1).sBeat = L".X.X..X..X.X....";
							seq.vecChannel.at(2).sBeat = L"X..X..X.X...X..X";
						}
						else if (slow == true)
						{
							seq.vecChannel.at(0).sBeat = L"X..............X";
							seq.vecChannel.at(1).sBeat = L"..X.......X...X.";
							seq.vecChannel.at(2).sBeat = L"X.....X........X";
						}
						else
						{
							answer = MessageBoxW(hWnd, L"You Have Not Selected A Beat Type!", L"Wait!", MB_ABORTRETRYIGNORE | MB_ICONEXCLAMATION);
							switch (answer)
							{
								case IDABORT:
									DestroyWindow(hWnd);
									break;
								case IDRETRY:
									return 0;
									break;
								case IDIGNORE:
									break;
							}
						}


						// Get all sound hardware
						vector<wstring> devices = olcNoiseMaker<short>::Enum();

						// Create sound machine!!
						olcNoiseMaker<short> sound(devices[0], 44100, 1, 8, 256);

						// Link noise function with sound machine
						sound.SetUserFunction(MakeNoise);



						auto clock_old_time = chrono::high_resolution_clock::now();
						auto clock_real_time = chrono::high_resolution_clock::now();
						double dElapsedTime = 0.0;
						double dWallTime = 0.0;

						while (1)
						{
							// --- SOUND STUFF ---

							// Update Timings =======================================================================================
							clock_real_time = chrono::high_resolution_clock::now();
							auto time_last_loop = clock_real_time - clock_old_time;
							clock_old_time = clock_real_time;
							dElapsedTime = chrono::duration<FTYPE>(time_last_loop).count();
							dWallTime += dElapsedTime;
							FTYPE dTimeNow = sound.GetTime();

							// Sequencer (generates notes, note offs applied by note lifespan) ======================================
							int newNotes = seq.Update(dElapsedTime);
							muxNotes.lock () ;
							for (int a = 0; a < newNotes; a++)
							{
								seq.vecNotes[a].on = dTimeNow;
								vecNotes.emplace_back(seq.vecNotes[a]);
							}
							muxNotes.unlock();

							// Keyboard (generates and removes notes depending on key state) ========================================
							for (int k = 0; k < 32; k++)
							{
								short nKeyState = GetAsyncKeyState((unsigned char)("ZSXCFVGBNJMK\xbcL\xbe\xbf\Q2W3ER5T6Y7UIO\\"[k]));

							if (GetAsyncKeyState(VK_ESCAPE))
							{
								Exit = true;
							}
							else
							{

							}
							if (Exit == true)
							{
								Exit2 = true;

							}
							else
								// Check if note already exists in currently playing notes
								muxNotes.lock () ;
							auto noteFound = find_if(vecNotes.begin(), vecNotes.end(), [&k](synth::note const&item) { return item.id == k + 64 && item.channel == &instHarmonica; });
							if (noteFound == vecNotes.end())
							{
								// Note not found in vector
								if (nKeyState & 0x8000)
								{
									// Key has been pressed so create a new note
									synth::note n;
									n.id = k + 64;
									n.on = dTimeNow;
									n.active = true;
									if (Harmonicaa == true)
									{
										n.channel = &instHarmonica;
									}
									else if (MPiano == true)
									{
										n.channel = &instPiano;
									}
									else if (Retro == true)
									{
										n.channel = &instHarmonica;
									}
									// Add note to vector
									vecNotes.emplace_back(n);

								}
							}
							else
							{
								// Note exists in vector
								if (nKeyState & 0x8000)
								{
									// Key is still held, so do nothing
									if (noteFound->off > noteFound->on)
									{
										// Key has been pressed again during release phase
										noteFound->on = dTimeNow;
										noteFound->active = true;
									}
								}
								else
								{
									// Key has been released, so switch off
									if (noteFound->off < noteFound->on)
										noteFound->off = dTimeNow;
								}
							}
							muxNotes.unlock();

						}




					}


					return 0;
			}

	}




	break;
	case WM_CREATE:
	LoadImages();
	AddMenus(hWnd);
	AddControls(hWnd);
	break;

	case WM_DESTROY:
	PostQuitMessage(0);
	break;
	default:
		return DefWindowProcW(hWnd, msg, wp, lp);

}
}

void AddMenus(HWND hWnd)
{
	hMenu = CreateMenu();
	HMENU hFileMenu = CreateMenu();
	AppendMenu(hFileMenu, MF_SEPARATOR, NULL, NULL);
	AppendMenu(hFileMenu, MF_STRING, FILE_MENU_EXIT, L"Exit");
	AppendMenu(hMenu, MF_POPUP, (UINT_PTR)hFileMenu, L"File");
	AppendMenu(hMenu, MF_STRING, FILE_MENU_HELP, L"Help");
	SetMenu(hWnd, hMenu);

}


void AddControls(HWND hWnd)
{
	CreateWindowW(L"Static", L"Music Synthesizer:", WS_VISIBLE | WS_CHILD | SS_CENTER | WS_BORDER, 600, 100, 100, 40, hWnd, NULL, NULL, NULL);
	hEdit = CreateWindowW(L"Edit", L"Aixzyl's", WS_VISIBLE | WS_CHILD | SS_CENTER | WS_BORDER | ES_AUTOHSCROLL, 600, 85, 100, 18, hWnd, NULL, NULL, NULL);
	CreateWindowW(L"Button", L"Change Title", WS_VISIBLE | WS_CHILD, 600, 10, 100, 50, hWnd, (HMENU)CHANGE_TITLE, NULL, NULL);
	hbitmapdisplay = CreateWindowW(L"Static", NULL, WS_VISIBLE | WS_CHILD | SS_BITMAP | WS_BORDER, 180, 200, 400, 100, hWnd, NULL, NULL, NULL);
	SendMessage(hbitmapdisplay, STM_SETIMAGE, IMAGE_BITMAP, (LPARAM)hmybitmap);
	CreateWindowW(L"Button", L"Produce Sound", WS_VISIBLE | WS_CHILD, 600, 150, 100, 50, hWnd, (HMENU)ProduceMusic, NULL, NULL);
	CreateWindowW(L"Button", L"Slow Beat", WS_VISIBLE | WS_CHILD, 200, 100, 100, 50, hWnd, (HMENU)SlowBeat, NULL, NULL);
	CreateWindowW(L"Button", L"Medium Beat", WS_VISIBLE | WS_CHILD, 300, 100, 100, 50, hWnd, (HMENU)MediumBeat, NULL, NULL);
	CreateWindowW(L"Button", L"Fast Beat", WS_VISIBLE | WS_CHILD, 400, 100, 100, 50, hWnd, (HMENU)FastBeat, NULL, NULL);
	CreateWindowW(L"Button", L"Melodic Piano", WS_VISIBLE | WS_CHILD, 800, 100, 100, 50, hWnd, (HMENU)MelodicPiano, NULL, NULL);
	CreateWindowW(L"Button", L"Retro Sound", WS_VISIBLE | WS_CHILD, 900, 100, 100, 50, hWnd, (HMENU)RetroSound, NULL, NULL);
	CreateWindowW(L"Button", L"Harmonica", WS_VISIBLE | WS_CHILD, 1000, 100, 100, 50, hWnd, (HMENU)Harmonica, NULL, NULL);



}
void AddHelpMenus(HWND hWnd)
{

	hMenu = CreateMenu();
	HMENU hFileMenu = CreateMenu();

	AppendMenu(hFileMenu, MF_SEPARATOR, NULL, NULL);
	AppendMenu(hFileMenu, MF_STRING, FILE_MENU_EXIT, L"Exit");
	AppendMenu(hMenu, MF_POPUP, (UINT_PTR)hFileMenu, L"File");

	SetMenu(hWnd, hMenu);

}

void LoadImages()
{
	hmybitmap = (HBITMAP)LoadImageW(NULL, L"Pianokeys.bmp", IMAGE_BITMAP, 1000, 400, LR_LOADFROMFILE);
}

LRESULT CALLBACK HelpClassProcedure(HWND hWnd, UINT msg, WPARAM wp, LPARAM lp)
{
	switch (msg)
	{
		case WM_CREATE:
			LoadImagesHelp();
			AddHelpMenus(hWnd);
			AddControlsHelp(hWnd);
			break;
		case WM_CLOSE:
			DestroyWindow(hWnd);
			break;
		default:
			return DefWindowProcW(hWnd, msg, wp, lp);
	}
}

void RegisterHelpClass(HINSTANCE hInst)
{
	WNDCLASSW HelpClass = { 0 };

	HelpClass.hbrBackground = (HBRUSH)COLOR_WINDOW;
	HelpClass.hCursor = LoadCursor(NULL, IDC_CROSS);
	HelpClass.hInstance = hInst;
	HelpClass.lpszClassName = L"HelpClass";
	HelpClass.lpfnWndProc = HelpClassProcedure;

	RegisterClassW(&HelpClass);
}

void DisplayHelpClass(HWND hWnd)
{
	CreateWindowW(L"HelpClass", L"Help!", WS_VISIBLE | WS_OVERLAPPEDWINDOW, 400, 400, 200, 200, hWnd, NULL, NULL, NULL);
}

void LoadImagesHelp()
{
	hmybitmaphelp = (HBITMAP)LoadImageW(NULL, L"Help.bmp", IMAGE_BITMAP, 1000, 400, LR_LOADFROMFILE);
}

void AddControlsHelp(HWND hWnd)
{
	hbitmapdisplayHelp = CreateWindowW(L"Static", NULL, WS_VISIBLE | WS_CHILD | SS_BITMAP | WS_BORDER, 180, 20, 200, 200, hWnd, NULL, NULL, NULL);
	SendMessage(hbitmapdisplayHelp, STM_SETIMAGE, IMAGE_BITMAP, (LPARAM)hmybitmaphelp);
}


Forms:
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Media;
using System.IO;
using System.Runtime.InteropServices;

namespace WindowsFormsApp3
{
	public partial class MusicSynthesizer : Form
	{
		[DllImport("winmm.dll")]
		private static extern long mciSendString(string Command, StringBuilder retstring, int Returnlenth, IntPtr callback);
		private const int SAMPLE_RATE = 44100;
		private const short BITS_PER_SAMPLE = 16;

		public SoundPlayer Splayer { get; private set; }
		int a = 0;
		public MusicSynthesizer()
		{
			InitializeComponent();
			mciSendString("open new Type waveaudio alias recsound", null, 0, IntPtr.Zero);
			button4.Click += new EventHandler(this.button4Click);
		}


		private void button4Click(object sender, EventArgs e)
		{
			//throw new NotImplementedException();
			mciSendString("record recsound", null, 0, IntPtr.Zero);
			button3.Click += new EventHandler(this.button3Click);
		}

		private void button3Click(object sender, EventArgs e)
		{
			//throw new NotImplementedException();
			mciSendString("save recsound c:\\C#Music\\CreatedSound.wav", null, 0, IntPtr.Zero);
			mciSendString("close recsound ", null, 0, IntPtr.Zero);
		}
		private void button4CLick(object sender, EventArgs e)
		{
			InitializeComponent();
			//throw new NotImplementedException();
			mciSendString("open new Type waveaudio alias recsound", null, 0, IntPtr.Zero);
			button4.Click += new EventHandler(this.button4Click);
			a++;
		}


		private void PlayBackGroundMusic_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				Splayer = new SoundPlayer(ofd.FileName);
				Splayer.Play();
			}
		}

		public void PlayBackGroundMusicLoop_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				Splayer = new SoundPlayer(ofd.FileName);
				Splayer.PlayLooping();

			}

		}
		public void StopBackGroundMusic_Click(object sender, EventArgs e)
		{
			Splayer.Stop();
		}

		private void MusicSynthesizer_Load(object sender, EventArgs e)
		{

		}
	}

}




