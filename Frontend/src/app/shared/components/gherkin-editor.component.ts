import { Component, Input, Output, EventEmitter, OnInit, ViewChild, ElementRef, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'app-gherkin-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => GherkinEditorComponent),
      multi: true
    }
  ],
  template: `
    <div class="gherkin-editor-container border border-slate-300 rounded-lg overflow-hidden">
      <!-- Toolbar -->
      <div class="bg-slate-50 border-b border-slate-200 px-4 py-2 flex items-center gap-2">
        <button 
          type="button"
          (click)="insertTemplate('feature')"
          class="px-3 py-1 text-sm bg-white border border-slate-300 rounded hover:bg-slate-100 transition-colors">
          Feature
        </button>
        <button 
          type="button"
          (click)="insertTemplate('scenario')"
          class="px-3 py-1 text-sm bg-white border border-slate-300 rounded hover:bg-slate-100 transition-colors">
          Scenario
        </button>
        <button 
          type="button"
          (click)="insertTemplate('given')"
          class="px-3 py-1 text-sm bg-white border border-slate-300 rounded hover:bg-slate-100 transition-colors">
          Given
        </button>
        <button 
          type="button"
          (click)="insertTemplate('when')"
          class="px-3 py-1 text-sm bg-white border border-slate-300 rounded hover:bg-slate-100 transition-colors">
          When
        </button>
        <button 
          type="button"
          (click)="insertTemplate('then')"
          class="px-3 py-1 text-sm bg-white border border-slate-300 rounded hover:bg-slate-100 transition-colors">
          Then
        </button>
      </div>

      <!-- Editor -->
      <textarea
        #editor
        [(ngModel)]="value"
        (ngModelChange)="onContentChange($event)"
        [placeholder]="placeholder"
        class="w-full p-4 font-mono text-sm resize-none focus:outline-none"
        [style.height]="height"
        spellcheck="false">
      </textarea>

      <!-- Line numbers (optional) -->
      <div class="bg-slate-50 border-t border-slate-200 px-4 py-2 text-xs text-slate-500">
        {{ lineCount }} ligne(s) | {{ charCount }} caractère(s)
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }

    textarea {
      tab-size: 2;
      line-height: 1.6;
    }

    textarea::placeholder {
      color: #94a3b8;
    }
  `]
})
export class GherkinEditorComponent implements ControlValueAccessor, OnInit {
  @Input() placeholder: string = 'Écrivez votre scénario Gherkin ici...';
  @Input() height: string = '400px';
  @Output() contentChange = new EventEmitter<string>();
  
  @ViewChild('editor') editorRef!: ElementRef<HTMLTextAreaElement>;

  value: string = '';
  lineCount: number = 0;
  charCount: number = 0;

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnInit(): void {
    this.updateStats();
  }

  // ControlValueAccessor implementation
  writeValue(value: string): void {
    this.value = value || '';
    this.updateStats();
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  onContentChange(content: string): void {
    this.value = content;
    this.onChange(content);
    this.contentChange.emit(content);
    this.updateStats();
  }

  updateStats(): void {
    this.lineCount = this.value ? this.value.split('\n').length : 0;
    this.charCount = this.value ? this.value.length : 0;
  }

  insertTemplate(type: string): void {
    const templates: { [key: string]: string } = {
      feature: 'Feature: \n  \n  ',
      scenario: '\nScenario: \n  Given \n  When \n  Then \n',
      given: '\n  Given ',
      when: '\n  When ',
      then: '\n  Then '
    };

    const template = templates[type] || '';
    const textarea = this.editorRef.nativeElement;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const before = this.value.substring(0, start);
    const after = this.value.substring(end);

    this.value = before + template + after;
    this.onChange(this.value);
    this.contentChange.emit(this.value);
    this.updateStats();

    // Set cursor position after template
    setTimeout(() => {
      textarea.selectionStart = textarea.selectionEnd = start + template.length;
      textarea.focus();
    }, 0);
  }
}
