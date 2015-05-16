#include <Servo.h>

Servo servo;

void setup() {
  servo.attach(9);
  
  Serial.begin(9600);
}

void loop() {
  if (Serial.available() > 0) {
    servo.write((int)Serial.read());
  }
}
