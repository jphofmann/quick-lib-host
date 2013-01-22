<?php
print("Starting SOAP/WSDL test\n");
$options['soap_version'] = SOAP_1_1;
$options['trace'] = TRUE;
//$options['location'] = 'http://192.168.4.57:8088/joinus';
//$options['uri'] = 'http://tempuri.irg';
$client = new SoapClient(
'http://192.168.4.57:8088/joinus/soap11',$options);
//null,$options);
print("SOAP Client created\n");
var_dump($client->__getFunctions());
//exit(0);
//$result_array = $client->InventoryGetWSDL( array( "thing" => "moo-cow"));
try
{
$result_array = $client->QuickTest( array( "Name" => "moo-cow"));
}
catch( soapFault $sf)
{
  echo "failed?";
  echo "c " . $sf->faultcode . " str: " . $sf->faultstring . "\n";
}

print "\nRequest:\n";
var_dump( $client->__getLastRequest() );
print "\nRequest Headers:\n";
var_dump( $client->__getLastRequestHeaders() );
print "\nResponse:\n";
var_dump( $client->__getLastResponse() );
print "\nResponse Headers:\n";
var_dump( $client->__getLastResponseHeaders() );

print "Result:\n";
print_r( $result_array);
print "-----\n";

print "Dumped:\n";
foreach( $result_array as $k=>$v )
{
   print $k . "\n";
   var_dump( $v );
}
print "-----\n";

print "Result: $result_array";
print "Done.\n";
//exit(0);
$json_content = get_url( "http://192.168.4.57:8088/joinus/hello/veliocrapter?format=json");

print "From JSON - " . $json_content . ",dumped," .  var_dump( $json_content );

  /* utility function:  go get it! */
  function get_url($url) {
    $ch = curl_init();
    curl_setopt($ch,CURLOPT_URL,$url);
    curl_setopt($ch,CURLOPT_HEADER,'Content-Type: application/json');
    curl_setopt($ch,CURLOPT_RETURNTRANSFER,1);
    curl_setopt($ch,CURLOPT_CONNECTTIMEOUT,1);
    $content = curl_exec($ch);
    curl_close($ch);
    return $content;
  }
?>

