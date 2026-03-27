import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DafComponent } from './daf.component';

describe('DafComponent', () => {
  let component: DafComponent;
  let fixture: ComponentFixture<DafComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DafComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DafComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
