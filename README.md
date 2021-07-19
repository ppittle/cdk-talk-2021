# Sample Project

1. Web - Hosts main site.  Including the 'Button'.  Also queries data from DynamoDB.
2. IngressLambda - Clicking the 'Button' calls this Api Gateway + Lambda function.  Puts data on processing SQS.
3. ProcessingLambda - listens to SQS.  Puts data into DynamoDB
4. CDK - Deploys all the things.  Also seeds DynamoDB with some sample data.