///////////////////////////////////////////////////////////////////////////////////////////////////
//
// file aerofly_fs_2_external_dll_sample.cpp
//
// PLEASE NOTE:  THE INTERFACE IN THIS FILE AND ALL DATA TYPES COULD BE SUBJECT TO SUBSTANTIAL
//               CHANGES WHILE AEROFLY FS 2 IS STILL RECEIVING UPDATES
//
// FURTHER NOTE: This sample just shows you how to read and send messages from the simulation
//               Some sample code is provided so see how to read and send messages
//
// 2019/12/19 - th/mb
//
// ---------------------------------------------------------------------------
//
// copyright (C) 2005-2017, Dr. Torsten Hans, Dr. Marc Borchers
// All rights reserved.
//
// Redistribution  and  use  in  source  and  binary  forms,  with  or  without
// modification, are permitted provided that the following conditions are met:
//
//  - Redistributions of  source code must  retain the above  copyright notice,
//    this list of conditions and the disclaimer below.
//  - Redistributions in binary form must reproduce the above copyright notice,
//    this  list of  conditions  and  the  disclaimer (as noted below)  in  the
//    documentation and/or other materials provided with the distribution.
//  - Neither the name of the copyright holder nor the names of its contributors
//    may be used to endorse or promote products derived from this software
//    without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT  HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR  IMPLIED WARRANTIES, INCLUDING,  BUT NOT  LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND  FITNESS FOR A PARTICULAR  PURPOSE
// ARE  DISCLAIMED. 
//
///////////////////////////////////////////////////////////////////////////////////////////////////

#if defined(WIN32) || defined(WIN64)
  #if defined(_MSC_VER)
    #pragma warning ( disable : 4530 )  // C++ exception handler used, but unwind semantics are not enabled
    #pragma warning ( disable : 4577 )  // 'noexcept' used with no exception handling mode specified; termination on exception is not guaranteed. Specify /EHsc
  #endif
#endif

#include "../shared/input/tm_external_message.h"

#include <WS2tcpip.h>
#include <windows.h>
#include <thread>
#include <vector>
#include <mutex>
#include <string>
#pragma comment(lib, "ws2_32.lib")

static HINSTANCE global_hDLLinstance = NULL;


//////////////////////////////////////////////////////////////////////////////////////////////////
//
// some ugly macros. we use this to be able to translate from string hash id to string
//
//////////////////////////////////////////////////////////////////////////////////////////////////
#define TM_MESSAGE( a1, a2, a3, a4, a5, a6, a7 )       static tm_external_message Message##a1( ##a2, a3, a4, a5, a6 );
#define TM_MESSAGE_NAME( a1, a2, a3, a4, a5, a6, a7 )  a2,


//////////////////////////////////////////////////////////////////////////////////////////////////
//
// list of messages that can be send/received
// to easy the interpretation of the messages, type, access flags and units are specified
//
//////////////////////////////////////////////////////////////////////////////////////////////////
#define MESSAGE_LIST(F) \
F( AircraftAltitude               , "Aircraft.Altitude"               , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Meter                        , "altitude as measured by altimeter                                                                            " ) \
F( AircraftVerticalSpeed          , "Aircraft.VerticalSpeed"          , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecond               , "vertical speed                                                                                               " ) \
F( AircraftPitch                  , "Aircraft.Pitch"                  , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Radiant                      , "pitch angle                                                                                                  " ) \
F( AircraftBank                   , "Aircraft.Bank"                   , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Radiant                      , "bank angle                                                                                                   " ) \
F( AircraftIndicatedAirspeed      , "Aircraft.IndicatedAirspeed"      , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecond               , "indicated airspeed                                                                                           " ) \
F( AircraftGroundSpeed            , "Aircraft.GroundSpeed"            , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecond               , "ground speed                                                                                                 " ) \
F( AircraftMagneticHeading        , "Aircraft.MagneticHeading"        , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Radiant                      , "                                                                                                             " ) \
F( AircraftTrueHeading            , "Aircraft.TrueHeading"            , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Radiant                      , "                                                                                                             " ) \
F( AircraftHeight                 , "Aircraft.Height"                 , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Meter                        , "                                                                                                             " ) \
F( AircraftPosition               , "Aircraft.Position"               , tm_msg_data_type::Vector3d     , tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::Meter                        , "                                                                                                             " ) \
F( AircraftOrientation            , "Aircraft.Orientation"            , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::None                         , "                                                                                                             " ) \
F( AircraftVelocity               , "Aircraft.Velocity"               , tm_msg_data_type::Vector3d     , tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecond               , "velocity vector         in body system if 'Body' flag is set, in global system otherwise                     " ) \
F( AircraftAngularVelocity        , "Aircraft.AngularVelocity"        , tm_msg_data_type::Vector3d     , tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::RadiantPerSecond             , "angular velocity        in body system if 'Body' flag is set (roll rate pitch rate yaw rate) in global system" ) \
F( AircraftAcceleration           , "Aircraft.Acceleration"           , tm_msg_data_type::Vector3d     , tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecondSquared        , "aircraft acceleration   in body system if 'Body' flag is set, in global system otherwise                     " ) \
F( AircraftGravity                , "Aircraft.Gravity"                , tm_msg_data_type::Vector3d     , tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecondSquared        , "gravity acceleration    in body system if 'Body' flag is set                                                 " ) \
F( AircraftWind                   , "Aircraft.Wind"                   , tm_msg_data_type::Vector3d     , tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::MeterPerSecond               , "wind vector at current aircraft position                                                                     " ) \
F( AircraftRateOfTurn             , "Aircraft.RateOfTurn"             , tm_msg_data_type::Double     ,   tm_msg_flag::Value , tm_msg_access::Read     , tm_msg_unit::RadiantPerSecond             , "rate of turn                                                                                                 " )
MESSAGE_LIST( TM_MESSAGE )

static std::vector<tm_external_message>  MessageListReceive;
static std::vector<tm_external_message>  MessageListCopy;
static std::vector<tm_external_message>  MessageListDebugOutput;
static std::mutex                        MessageListMutex;
static double                            MessageDeltaTime = 0;


static WSADATA wsaData;

static SOCKET SendSocket;
static sockaddr_in RecvAddr;
static unsigned short Port;


static HANDLE hFile;
static HANDLE hAppend;
static DWORD  dwBytesRead, dwBytesWritten, dwPos;
static BYTE   buff[4096];

static SYSTEMTIME mytime;



//////////////////////////////////////////////////////////////////////////////////////////////////
//
// a small helper function that shows the name of a message as plain text if an ID is passed
//
//////////////////////////////////////////////////////////////////////////////////////////////////
struct tm_message_type
{
  tm_string       String;
  tm_string_hash  StringHash;
  template <tm_uint32 N> constexpr tm_message_type( const char( &str )[N] ) : String{ str }, StringHash{ str } { }
};

static std::vector<tm_message_type> MessageTypeList = 
{
  MESSAGE_LIST( TM_MESSAGE_NAME )
};

static tm_string GetMessageName( const tm_external_message &message )
{
  for( const auto &mt : MessageTypeList )
  {
    if( message.GetID() == mt.StringHash.GetHash() ) { return mt.String; }
  }

  return tm_string( "unknown" );
}


//////////////////////////////////////////////////////////////////////////////////////////////////
//
// the main entry point for the DLL
//
//////////////////////////////////////////////////////////////////////////////////////////////////
BOOL WINAPI DllMain( HANDLE hdll, DWORD reason, LPVOID reserved )
{
  switch ( reason )
  {
    case DLL_THREAD_ATTACH:
      break;
    case DLL_THREAD_DETACH:
      break;
    case DLL_PROCESS_ATTACH:
      global_hDLLinstance = (HINSTANCE) hdll;
      break;
    case DLL_PROCESS_DETACH:
      break;
  }

  return TRUE;
}




//////////////////////////////////////////////////////////////////////////////////////////////////
//
// interface functions to Aerofly FS 2
//
//////////////////////////////////////////////////////////////////////////////////////////////////
extern "C" 
{
  __declspec( dllexport ) int Aerofly_FS_2_External_DLL_GetInterfaceVersion()
  {
    return TM_DLL_INTERFACE_VERSION;
  }

  __declspec( dllexport ) bool Aerofly_FS_2_External_DLL_Init( const HINSTANCE Aerofly_FS_2_hInstance )
  {
	  // Initialize Winsock
	  WSAStartup(MAKEWORD(2, 2), &wsaData);
	  SendSocket = INVALID_SOCKET;
	  Port = 4123;
	  SendSocket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);

	  RecvAddr.sin_family = AF_INET;
	  RecvAddr.sin_port = htons(Port);
	  RecvAddr.sin_addr.s_addr = inet_addr("127.0.0.1");


	  hAppend = CreateFile(TEXT("D:\\two.txt"), // open Two.txt
		  FILE_APPEND_DATA,         // open for writing
		  FILE_SHARE_READ,          // allow multiple readers
		  NULL,                     // no security
		  OPEN_ALWAYS,              // open or create
		  FILE_ATTRIBUTE_NORMAL,    // normal file
		  NULL);                    // no attr. template
  	
    return true;
  }
  
  __declspec( dllexport ) void Aerofly_FS_2_External_DLL_Shutdown()
  {
  }

  int resolvehelper(const char* hostname, int family, const char* service, sockaddr_storage* pAddr)
  {
	  int result = 0;
	  addrinfo* result_list = NULL;
	  addrinfo hints = {};
	  hints.ai_family = family;
	  hints.ai_socktype = SOCK_DGRAM; // without this flag, getaddrinfo will return 3x the number of addresses (one for each socket type).
	  result = getaddrinfo(hostname, service, &hints, &result_list);
	  if (result == 0)
	  {
		  //ASSERT(result_list->ai_addrlen <= sizeof(sockaddr_in));
		  memcpy(pAddr, result_list->ai_addr, result_list->ai_addrlen);
		  freeaddrinfo(result_list);
	  }

	  return result;
  }
  
  __declspec( dllexport ) void Aerofly_FS_2_External_DLL_Update( const tm_double         delta_time,
                                                                 const tm_uint8 * const  message_list_received_byte_stream,
                                                                 const tm_uint32         message_list_received_byte_stream_size,
                                                                 const tm_uint32         message_list_received_num_messages,
                                                                 tm_uint8               *message_list_sent_byte_stream,
                                                                 tm_uint32              &message_list_sent_byte_stream_size,
                                                                 tm_uint32              &message_list_sent_num_messages,
                                                                 const tm_uint32         message_list_sent_byte_stream_size_max )
  {
    //////////////////////////////////////////////////////////////////////////////////////////////
    //
    // build a list of messages that the simulation is sending
    //


    MessageListReceive.clear();

    tm_uint32 message_list_received_byte_stream_pos = 0;
    for ( tm_uint32 i = 0; i < message_list_received_num_messages; ++i ) {
      auto edm = tm_external_message::GetFromByteStream( message_list_received_byte_stream, message_list_received_byte_stream_pos );
      MessageListReceive.push_back( edm );
    }
	

    //////////////////////////////////////////////////////////////////////////////////////////////
    //
    // parse the message list
    //
    
    tm_vector3d aircraft_position;
	tm_double aircraft_pitch;
    tm_double aircraft_bank;
	tm_double aircraft_indicated_airspeed;
	tm_double aircraft_groundspeed;
  	tm_double aircraft_rateofturn;
	tm_vector3d aircraft_angularvelocity;
	tm_vector3d aircraft_acceleration;
	tm_vector3d aircraft_velocity;
	tm_vector3d aircraft_gravity;
	
    for ( const auto &message : MessageListReceive ) {
	  if (message.GetStringHash() == "Aircraft.Pitch") { aircraft_pitch = message.GetDouble(); }
	  else if (message.GetStringHash() == "Aircraft.Bank") { aircraft_bank = message.GetDouble();  if (aircraft_bank > 3) aircraft_bank -= 6; }
	  else if (message.GetStringHash() == "Aircraft.RateOfTurn") { aircraft_rateofturn = message.GetDouble(); }
	  else if (message.GetStringHash() == "Aircraft.AngularVelocity") { aircraft_angularvelocity = message.GetVector3d(); } //Aircraft.Acceleration would be a better information, but the api gives only 1 value per secound
	  else if (message.GetStringHash() == "Aircraft.Velocity") { aircraft_velocity = message.GetVector3d(); }
	  else if (message.GetStringHash() == "Aircraft.IndicatedAirspeed") { aircraft_indicated_airspeed = message.GetDouble(); }
	  else if (message.GetStringHash() == "Aircraft.GroundSpeed") { aircraft_groundspeed = message.GetDouble(); }
      // for possible values see list of messages at line 73 and following ...
    }


	//////////////////////////////////////////////////////////////////////////////////////////////
	//
	// send selected data to udp port to localhost:4123 
	//
  	

		char msg[256]="";
		sprintf_s(msg, "%lld;%lld;%lld;%lld;%lld;%lld;%lld;%lld;%lld;%lld;%lld",
				(INT64)(aircraft_pitch * 1000),
				(INT64)(aircraft_bank * 1000),
				(INT64)(aircraft_rateofturn * 1000),
				(INT64)(aircraft_angularvelocity.x * 1000), // 3
				(INT64)(aircraft_angularvelocity.y * 1000),
				(INT64)(aircraft_angularvelocity.z * 1000),
				(INT64)(aircraft_velocity.x * 1000),	// 6
				(INT64)(aircraft_velocity.y * 1000),
				(INT64)(aircraft_velocity.z * 1000),			
				(INT64)(aircraft_indicated_airspeed * 1000),
				(INT64)(aircraft_groundspeed * 1000)
				);
		int msg_length = (int)strlen(msg);
		sendto(SendSocket, msg, msg_length, 0, (SOCKADDR*)&RecvAddr, sizeof(RecvAddr));

		/*
		GetSystemTime(&mytime);
		LONG time_ms = (mytime.wSecond * 1000) + mytime.wMilliseconds;
		std::string curr_time_str = std::to_string(time_ms) + "\r\n";
		WriteFile(hAppend, curr_time_str.c_str(), strlen(curr_time_str.c_str()), &dwBytesWritten, NULL);
		*/
  	
  }

}
