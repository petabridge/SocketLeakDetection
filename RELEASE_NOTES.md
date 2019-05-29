#### 0.1.0 May 29 2019 ####
First release-

This release introduces a tool designed to help detect a sudden increase in the TCP port count in a system, and gracefully shutdown the ActorSystem when this is observed. The configuration can be set to monitor slow increases or fast spikes in TCP port growth for one or more local endpoints. 