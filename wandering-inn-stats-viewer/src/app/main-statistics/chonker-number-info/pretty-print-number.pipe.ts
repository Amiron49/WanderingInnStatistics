import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'prettyPrintNumber'
})
export class PrettyPrintNumberPipe implements PipeTransform {

  transform(value: number): string {

    let formatter = new Intl.NumberFormat('en-us');

    return formatter.format(value);
  }

}
