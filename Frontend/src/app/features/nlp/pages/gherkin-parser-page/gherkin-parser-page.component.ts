import { Component } from '@angular/core';
import { GherkinParserComponent } from '../../components/gherkin-parser/gherkin-parser.component';

@Component({
  selector: 'app-gherkin-parser-page',
  standalone: true,
  imports: [GherkinParserComponent],
  template: '<app-gherkin-parser></app-gherkin-parser>'
})
export class GherkinParserPageComponent {}
