# pass-application
 Application for creating and managing citizens passport application. Demonstrates features of service fabric.


This application is designed to serve as onboarding application and to demonstrate usage of some of the Service Fabric features.
The main purpose of application is to enable citizen to apply for passport. There are a few different roles in this system:

1.	Citizen – citizen can get their data from the system via unique ID number (jmbg). Citizen can view their data and apply for passport with it. Also, citizen can view all their previous passport applications.
2.	Enrolment officer – officer is responsible for entering citizens data into the system
3.	Approval officer – officer responsible for approving passport applications. 
Role of approval officers will be mocked in this system 


Service	list
Citizen Data	
	Store citizen data in reliable dictionary. 
	Provide REST API to fetch and enter citizen data
Application Data
	Store application data in reliable dictionary. 
	Provide REST API to fetch and enter/update application data
Application Web
	Public API used by citizens. 
	-Feth citizen data
	-Create new passport application
	-View all of the applications for given citizen
AdminAPI
	Private API of the system (used by officers)
	-	Enroll new citizens data into the system
	-	View all of the applications in the system
	-	Update state of the given application
ApplicationApprover	
	Mock service used to emulate approval officer
	-	Fetch all of the application in status “Created”
	-	Change state of confiugred number of applications


Service Fabrice features demonstration
1.	Partitioning and replication 
2.	Custom metrics
3.	Autoscaling
4.	Node placement

